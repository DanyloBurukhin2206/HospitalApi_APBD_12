using HospitalApi.Data;
using HospitalApi.Dtos;
using HospitalApi.Models;
using Microsoft.EntityFrameworkCore;

namespace HospitalApi.Services;

public interface IPatientService
{
    Task<List<PatientResponseDto>> GetPatientsAsync(string? search);
    Task<CreateBedAssignmentResult> CreateBedAssignmentAsync(string pesel, CreateBedAssignmentRequestDto request);
}

public class PatientService : IPatientService
{
    private readonly HospitalDbContext _context;

    public PatientService(HospitalDbContext context)
    {
        _context = context;
    }

    public async Task<List<PatientResponseDto>> GetPatientsAsync(string? search)
    {
        var query = _context.Patients
            .Include(p => p.Admissions)
                .ThenInclude(a => a.Ward)
            .Include(p => p.BedAssignments)
                .ThenInclude(ba => ba.Bed)
                    .ThenInclude(b => b.BedType)
            .Include(p => p.BedAssignments)
                .ThenInclude(ba => ba.Bed)
                    .ThenInclude(b => b.Room)
                        .ThenInclude(r => r.Ward)
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = $"%{search.Trim()}%";
            query = query.Where(p =>
                EF.Functions.Like(p.FirstName, pattern) ||
                EF.Functions.Like(p.LastName, pattern));
        }

        var patients = await query
            .OrderBy(p => p.LastName)
            .ThenBy(p => p.FirstName)
            .ToListAsync();

        return patients.Select(MapPatient).ToList();
    }

    public async Task<CreateBedAssignmentResult> CreateBedAssignmentAsync(string pesel, CreateBedAssignmentRequestDto request)
    {
        if (string.IsNullOrWhiteSpace(pesel))
            return CreateBedAssignmentResult.BadRequest("PESEL pacjenta jest wymagany.");

        if (request.To.HasValue && request.To.Value <= request.From)
            return CreateBedAssignmentResult.BadRequest("Data 'to' musi być późniejsza niż data 'from'.");

        var normalizedPesel = pesel.Trim();
        var patientExists = await _context.Patients.AnyAsync(p => p.Pesel == normalizedPesel);
        if (!patientExists)
            return CreateBedAssignmentResult.NotFound($"Pacjent o numerze PESEL '{normalizedPesel}' nie istnieje.");

        var wardName = request.Ward.Trim();
        var bedTypeName = request.BedType.Trim();

        var wardExists = await _context.Wards.AnyAsync(w => w.Name == wardName);
        if (!wardExists)
            return CreateBedAssignmentResult.NotFound($"Oddział '{wardName}' nie istnieje.");

        var bedTypeExists = await _context.BedTypes.AnyAsync(bt => bt.Name == bedTypeName);
        if (!bedTypeExists)
            return CreateBedAssignmentResult.NotFound($"Typ łóżka '{bedTypeName}' nie istnieje.");

        IQueryable<Bed> bedsQuery = _context.Beds
            .Include(b => b.BedType)
            .Include(b => b.Room)
                .ThenInclude(r => r.Ward)
            .Where(b => b.BedType.Name == bedTypeName && b.Room.Ward.Name == wardName);

        bedsQuery = request.To.HasValue
            ? bedsQuery.Where(b => !b.BedAssignments.Any(ba =>
                ba.From < request.To.Value &&
                (!ba.To.HasValue || ba.To.Value > request.From)))
            : bedsQuery.Where(b => !b.BedAssignments.Any(ba =>
                !ba.To.HasValue || ba.To.Value > request.From));

        var freeBed = await bedsQuery
            .OrderBy(b => b.Id)
            .FirstOrDefaultAsync();

        if (freeBed is null)
            return CreateBedAssignmentResult.NotFound(
                $"Brak wolnego łóżka typu '{bedTypeName}' na oddziale '{wardName}' w podanym terminie.");

        var assignment = new BedAssignment
        {
            PatientPesel = normalizedPesel,
            BedId = freeBed.Id,
            From = request.From,
            To = request.To
        };

        _context.BedAssignments.Add(assignment);
        await _context.SaveChangesAsync();

        var createdAssignment = await _context.BedAssignments
            .Include(ba => ba.Bed)
                .ThenInclude(b => b.BedType)
            .Include(ba => ba.Bed)
                .ThenInclude(b => b.Room)
                    .ThenInclude(r => r.Ward)
            .AsNoTracking()
            .FirstAsync(ba => ba.Id == assignment.Id);

        return CreateBedAssignmentResult.Created(MapCreatedAssignment(createdAssignment));
    }

    private static PatientResponseDto MapPatient(Patient patient)
    {
        return new PatientResponseDto
        {
            Pesel = patient.Pesel,
            FirstName = patient.FirstName,
            LastName = patient.LastName,
            Age = patient.Age,
            Sex = patient.Sex ? "Male" : "Female",
            Admissions = patient.Admissions
                .OrderBy(a => a.AdmissionDate)
                .Select(a => new AdmissionDto
                {
                    Id = a.Id,
                    AdmissionDate = a.AdmissionDate,
                    DischargeDate = a.DischargeDate,
                    Ward = MapWard(a.Ward)
                }).ToList(),
            BedAssignments = patient.BedAssignments
                .OrderBy(ba => ba.From)
                .Select(MapBedAssignment)
                .ToList()
        };
    }

    private static BedAssignmentDto MapBedAssignment(BedAssignment assignment)
    {
        return new BedAssignmentDto
        {
            Id = assignment.Id,
            From = assignment.From,
            To = assignment.To,
            Bed = MapBed(assignment.Bed)
        };
    }

    private static CreateBedAssignmentResponseDto MapCreatedAssignment(BedAssignment assignment)
    {
        return new CreateBedAssignmentResponseDto
        {
            Id = assignment.Id,
            PatientPesel = assignment.PatientPesel,
            From = assignment.From,
            To = assignment.To,
            Bed = MapBed(assignment.Bed)
        };
    }

    private static BedDto MapBed(Bed bed)
    {
        return new BedDto
        {
            Id = bed.Id,
            BedType = new BedTypeDto
            {
                Id = bed.BedType.Id,
                Name = bed.BedType.Name,
                Description = bed.BedType.Description
            },
            Room = new RoomDto
            {
                Id = bed.Room.Id,
                HasTv = bed.Room.HasTv,
                Ward = MapWard(bed.Room.Ward)
            }
        };
    }

    private static WardDto MapWard(Ward ward)
    {
        return new WardDto
        {
            Id = ward.Id,
            Name = ward.Name,
            Description = ward.Description
        };
    }
}

public class CreateBedAssignmentResult
{
    private CreateBedAssignmentResult(int statusCode, string? errorMessage, CreateBedAssignmentResponseDto? value)
    {
        StatusCode = statusCode;
        ErrorMessage = errorMessage;
        Value = value;
    }

    public int StatusCode { get; }
    public string? ErrorMessage { get; }
    public CreateBedAssignmentResponseDto? Value { get; }

    public static CreateBedAssignmentResult Created(CreateBedAssignmentResponseDto value) => new(201, null, value);
    public static CreateBedAssignmentResult BadRequest(string message) => new(400, message, null);
    public static CreateBedAssignmentResult NotFound(string message) => new(404, message, null);
}
