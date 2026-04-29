using System.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Przychodnia_APBD_cw6.DTOs;

namespace Przychodnia_APBD_cw6.Services;

public class AppointmentService(IConfiguration configuration) : IAppointmentService
{
    
    public async Task<List<AppointmentListDto>> GetAppointments(
        [FromQuery] string? status,
        [FromQuery] string? patientLastName)
        {
            var result = new List<AppointmentListDto>();

            await using var connection = new SqlConnection(configuration.GetConnectionString("DefaultConnection"));
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

            return result;
        }

    [HttpGet("{idAppointment:int}")]
    public async Task<AppointmentDetailsDto> GetAppointment(int idAppointment)
    {
        await using var connection = new SqlConnection(configuration.GetConnectionString("DefaultConnection"));
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
                JOIN dbo.Doctors d ON d.IdDoctor = a.IdDoctor
                JOIN dbo.Specializations s ON d.IdSpecialization = s.IdSpecialization
                WHERE a.IdAppointment = @Id
            ",  connection);
        command.Parameters.Add("@Id", SqlDbType.Int).Value = idAppointment;
        
        await using var reader = await command.ExecuteReaderAsync();
        await reader.ReadAsync();

        if (!reader.HasRows)
        {
            return null;
        }

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

        return dto;
    }
}