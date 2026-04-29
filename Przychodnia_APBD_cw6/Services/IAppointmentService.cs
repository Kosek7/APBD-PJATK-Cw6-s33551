using Przychodnia_APBD_cw6.DTOs;

namespace Przychodnia_APBD_cw6.Services;

public interface IAppointmentService
{
    Task<List<AppointmentListDto>> GetAppointments(string? status, string? patientLastName);
    Task<AppointmentDetailsDto> GetAppointment(int id);
    Task<int> CreateAppointmentAsync(CreateAppointmentRequestDto dto);
    Task UpdateAppointmentAsync(int id, UpdateAppointmentRequestDto dto);
    Task DeleteAppointmentAsync(int id);
}
