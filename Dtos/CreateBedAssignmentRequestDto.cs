using System.ComponentModel.DataAnnotations;

namespace HospitalApi.Dtos;

public class CreateBedAssignmentRequestDto
{
    [Required]
    public DateTime From { get; set; }

    public DateTime? To { get; set; }

    [Required]
    public string BedType { get; set; } = null!;

    [Required]
    public string Ward { get; set; } = null!;
}
