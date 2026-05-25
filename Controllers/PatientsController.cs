using HospitalApi.Dtos;
using HospitalApi.Services;
using Microsoft.AspNetCore.Mvc;

namespace HospitalApi.Controllers;

[ApiController]
[Route("api/patients")]
public class PatientsController : ControllerBase
{
    private readonly IPatientService _patientService;

    public PatientsController(IPatientService patientService)
    {
        _patientService = patientService;
    }

    [HttpGet]
    public async Task<ActionResult<List<PatientResponseDto>>> GetPatients([FromQuery] string? search)
    {
        var patients = await _patientService.GetPatientsAsync(search);
        return Ok(patients);
    }

    [HttpPost("{pesel}/bedassignments")]
    public async Task<ActionResult<CreateBedAssignmentResponseDto>> CreateBedAssignment(
        [FromRoute] string pesel,
        [FromBody] CreateBedAssignmentRequestDto request)
    {
        var result = await _patientService.CreateBedAssignmentAsync(pesel, request);

        if (result.StatusCode == StatusCodes.Status400BadRequest)
            return BadRequest(new ErrorDto { Message = result.ErrorMessage! });

        if (result.StatusCode == StatusCodes.Status404NotFound)
            return NotFound(new ErrorDto { Message = result.ErrorMessage! });

        return Created($"/api/patients/{pesel}/bedassignments/{result.Value!.Id}", result.Value);
    }
}
