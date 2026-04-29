using Przychodnia_APBD_cw6.DTOs;

namespace Przychodnia_APBD_cw6.Controllers;

using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Data;


[ApiController]
[Route("api/[controller]")]

public class AppointmentsController : ControllerBase
{
    private readonly string _connectionString;
    
    public AppointmentsController(IConfiguration configuration)
        {
        _connectionString = configuration.GetConnectionString("DefaultConnection")!;
        }
    
    
    [HttpGet]
    public async Task<IActionResult> GetAppointments(
        [FromQuery] string? status,
        [FromQuery] string? patientLastName)
        {
            var result = new List<AppointmentListDto>();

            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            await using var command = new SqlCommand(@"
                SELECT
                    a.IdAppointment,
                    a.AppointmentDate,
                    a.Status,
                    a.Reason,
                    p.FirstName + ' ' + p.LastName AS PatientFullName,
                    p.Email AS PatientEmail
                FROM dbo.Appointments a
                JOIN dbo.Patients p ON p.IdPatient = a.IdPatient
                WHERE (@Status IS NULL OR a.Status = @Status)
                AND (@PatientLastName IS NULL OR p.LastName = @PatientLastName)
                ORDER BY a.AppointmentDate;
            ",  connection);
            
            command.Parameters.Add("@Status", SqlDbType.NVarChar, 30)
                .Value = (object?)status ?? DBNull.Value;
            
            command.Parameters.Add("@PatientLastName", SqlDbType.NVarChar, 80)
                .Value = (object?)patientLastName ?? DBNull.Value;

            await using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                result.Add(new AppointmentListDto
                {
                    IdAppointment = reader.GetInt32(0),
                    AppointmentDate = reader.GetDateTime(1),
                    Status = reader.GetString(2),
                    Reason = reader.GetString(3),
                    PatientFullName = reader.GetString(4),
                    PatientEmail = reader.GetString(5)
                });
            }
            
            return Ok(result);
        }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetAppointment(int idAppointment)
    {
        var result = new AppointmentDetailsDto();
        
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        
        await using var command = new SqlCommand(@"
                SELECT
                    a.IdAppointment,
                    a.AppointmentDate,
                    a.Status,
                    a.Reason,
                    p.FirstName + ' ' + p.LastName AS PatientFullName,
                    p.Email AS PatientEmail,
                    p.PhoneNumber,
                    d.FirstName + ' ' + d.LastName AS DoctorFullName,
                    d.LicenseNumber,
                    s.Name AS Specialization,
                    a.InternalNotes,
                    a.CreatedAt
                FROM dbo.Appointments a
                JOIN dbo.Patients p ON p.IdPatient = a.IdPatient
                JOIN dbo.Doctors d ON d.IdDoctor = p.IdDoctor
                JOIN dbo.Specializations s ON d.IdSpecialization = s.IdSpecialization
                WHERE a.IdAppointment = @Id
                ORDER BY a.AppointmentDate;
            ",  connection);
        command.Parameters.Add("@Id", SqlDbType.Int).Value = idAppointment;
        
        await using var reader = await command.ExecuteReaderAsync();

        if (!reader.HasRows)
        {
            return NotFound(new ErrorResponseDto
            {
                Message = "No appointment found"
            });
        }
        
        await reader.ReadAsync();

        var dto = new AppointmentDetailsDto
        {
            IdAppointment = reader.GetInt32(0),
            AppointmentDate = reader.GetDateTime(1),
            Status = reader.GetString(2),
            Reason = reader.GetString(3),

            PatientFullName = reader.GetString(4),
            PatientEmail = reader.GetString(5),
            PatientPhone = reader.GetString(6),

            DoctorFullName = reader.GetString(7),
            LicenseNumber = reader.GetString(8),

            Specialization = reader.GetString(9),

            InternalNotes = reader.IsDBNull(10) ? null : reader.GetString(10),
            CreatedAt = reader.GetDateTime(11)
        };

        return Ok(dto);
    }
}