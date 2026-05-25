namespace HospitalApi.Dtos;

public class CreateBedAssignmentResponseDto
{
    public int Id { get; set; }
    public string PatientPesel { get; set; } = null!;
    public DateTime From { get; set; }
    public DateTime? To { get; set; }
    public BedDto Bed { get; set; } = null!;
}
