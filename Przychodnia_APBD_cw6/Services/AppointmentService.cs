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
    
    public async Task<int> CreateAppointmentAsync(CreateAppointmentRequestDto dto)
{
    if (dto.AppointmentDate < DateTime.Now)
        throw new Exception("Appointment date cannot be in the past");

    if (string.IsNullOrWhiteSpace(dto.Reason) || dto.Reason.Length > 250)
        throw new Exception("Invalid reason");

    await using var connection = new SqlConnection(configuration.GetConnectionString("DefaultConnection"));
    await connection.OpenAsync();

    var patientCmd = new SqlCommand(@"
        SELECT IsActive
        FROM Patients
        WHERE IdPatient = @Id
    ", connection);

    patientCmd.Parameters.Add("@Id", SqlDbType.Int).Value = dto.IdPatient;

    var patient = await patientCmd.ExecuteScalarAsync();

    if (patient == null)
        throw new Exception("Patient not found");

    if (!(bool)patient)
        throw new Exception("Patient not active");

    var doctorCmd = new SqlCommand(@"
        SELECT IsActive
        FROM Doctors
        WHERE IdDoctor = @Id
    ", connection);

    doctorCmd.Parameters.Add("@Id", SqlDbType.Int).Value = dto.IdDoctor;

    var doctor = await doctorCmd.ExecuteScalarAsync();

    if (doctor == null)
        throw new Exception("Doctor not found");

    if (!(bool)doctor)
        throw new Exception("Doctor not active");

    var conflictCmd = new SqlCommand(@"
        SELECT COUNT(*)
        FROM Appointments
        WHERE IdDoctor = @Doctor
          AND AppointmentDate = @Date
    ", connection);

    conflictCmd.Parameters.Add("@Doctor", SqlDbType.Int).Value = dto.IdDoctor;
    conflictCmd.Parameters.Add("@Date", SqlDbType.DateTime2).Value = dto.AppointmentDate;

    var exists = (int)await conflictCmd.ExecuteScalarAsync();

    if (exists > 0)
        throw new Exception("Doctor already has appointment at this time");

    var insertCmd = new SqlCommand(@"
        INSERT INTO Appointments
        (IdPatient, IdDoctor, AppointmentDate, Status, Reason)
        VALUES
        (@Patient, @Doctor, @Date, 'Scheduled', @Reason);

        SELECT SCOPE_IDENTITY();
    ", connection);

    insertCmd.Parameters.Add("@Patient", SqlDbType.Int).Value = dto.IdPatient;
    insertCmd.Parameters.Add("@Doctor", SqlDbType.Int).Value = dto.IdDoctor;
    insertCmd.Parameters.Add("@Date", SqlDbType.DateTime2).Value = dto.AppointmentDate;
    insertCmd.Parameters.Add("@Reason", SqlDbType.NVarChar, 250).Value = dto.Reason;

    return Convert.ToInt32(await insertCmd.ExecuteScalarAsync());
}
    
    public async Task UpdateAppointmentAsync(int id, UpdateAppointmentRequestDto dto)
{
    await using var connection = new SqlConnection(configuration.GetConnectionString("DefaultConnection"));
    await connection.OpenAsync();

    var getCmd = new SqlCommand(@"
        SELECT Status, IdDoctor
        FROM Appointments
        WHERE IdAppointment = @Id
    ", connection);

    getCmd.Parameters.Add("@Id", SqlDbType.Int).Value = id;

    await using var reader = await getCmd.ExecuteReaderAsync();

    if (!await reader.ReadAsync())
        throw new Exception("Appointment not found");

    var currentStatus = reader.GetString(0);

    await reader.CloseAsync();

    if (dto.Status != "Scheduled" &&
        dto.Status != "Completed" &&
        dto.Status != "Cancelled")
        throw new Exception("Invalid status");

    if (currentStatus == "Completed" && dto.AppointmentDate != default)
        throw new Exception("Cannot modify completed appointment time");

    var patientCmd = new SqlCommand(@"
        SELECT IsActive FROM Patients WHERE IdPatient = @Id
    ", connection);

    patientCmd.Parameters.Add("@Id", SqlDbType.Int).Value = dto.IdPatient;

    var patient = await patientCmd.ExecuteScalarAsync();

    if (patient == null) throw new Exception("Patient not found");
    if (!(bool)patient) throw new Exception("Patient not active");

    var doctorCmd = new SqlCommand(@"
        SELECT IsActive FROM Doctors WHERE IdDoctor = @Id
    ", connection);

    doctorCmd.Parameters.Add("@Id", SqlDbType.Int).Value = dto.IdDoctor;

    var doctor = await doctorCmd.ExecuteScalarAsync();

    if (doctor == null) throw new Exception("Doctor not found");
    if (!(bool)doctor) throw new Exception("Doctor not active");

    var conflictCmd = new SqlCommand(@"
        SELECT COUNT(*)
        FROM Appointments
        WHERE IdDoctor = @Doctor
          AND AppointmentDate = @Date
          AND IdAppointment <> @Id
    ", connection);

    conflictCmd.Parameters.Add("@Doctor", SqlDbType.Int).Value = dto.IdDoctor;
    conflictCmd.Parameters.Add("@Date", SqlDbType.DateTime2).Value = dto.AppointmentDate;
    conflictCmd.Parameters.Add("@Id", SqlDbType.Int).Value = id;

    var conflict = (int)await conflictCmd.ExecuteScalarAsync();

    if (conflict > 0)
        throw new Exception("Doctor already has appointment at this time");

    var updateCmd = new SqlCommand(@"
        UPDATE Appointments
        SET IdPatient = @Patient,
            IdDoctor = @Doctor,
            AppointmentDate = @Date,
            Status = @Status,
            Reason = @Reason,
            InternalNotes = @Notes
        WHERE IdAppointment = @Id
    ", connection);

    updateCmd.Parameters.Add("@Patient", SqlDbType.Int).Value = dto.IdPatient;
    updateCmd.Parameters.Add("@Doctor", SqlDbType.Int).Value = dto.IdDoctor;
    updateCmd.Parameters.Add("@Date", SqlDbType.DateTime2).Value = dto.AppointmentDate;
    updateCmd.Parameters.Add("@Status", SqlDbType.NVarChar, 30).Value = dto.Status;
    updateCmd.Parameters.Add("@Reason", SqlDbType.NVarChar, 250).Value = dto.Reason;
    updateCmd.Parameters.Add("@Notes", SqlDbType.NVarChar, 500).Value = (object?)dto.InternalNotes ?? DBNull.Value;
    updateCmd.Parameters.Add("@Id", SqlDbType.Int).Value = id;

    await updateCmd.ExecuteNonQueryAsync();
}
    
    public async Task DeleteAppointmentAsync(int id)
    {
        await using var connection = new SqlConnection(configuration.GetConnectionString("DefaultConnection"));
        await connection.OpenAsync();

        var checkCmd = new SqlCommand(@"
        SELECT Status
        FROM Appointments
        WHERE IdAppointment = @Id
    ", connection);

        checkCmd.Parameters.Add("@Id", SqlDbType.Int).Value = id;

        var statusObj = await checkCmd.ExecuteScalarAsync();

        if (statusObj == null)
            throw new Exception("Appointment not found");

        var status = (string)statusObj;

        if (status == "Completed")
            throw new Exception("Cannot delete completed appointment");

        var deleteCmd = new SqlCommand(@"
        DELETE FROM Appointments
        WHERE IdAppointment = @Id
    ", connection);

        deleteCmd.Parameters.Add("@Id", SqlDbType.Int).Value = id;

        await deleteCmd.ExecuteNonQueryAsync();
    }
}