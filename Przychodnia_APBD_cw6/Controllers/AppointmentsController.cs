using Przychodnia_APBD_cw6.DTOs;
using Przychodnia_APBD_cw6.Services;

namespace Przychodnia_APBD_cw6.Controllers;

using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Data;


[ApiController]
[Route("api/appointments")]

public class AppointmentsController : ControllerBase
{
    private readonly IAppointmentService _service;
    
    public AppointmentsController(IAppointmentService service)
    {
        _service = service;
    }
    
    [HttpGet]
    public async Task<IActionResult> GetAppointments(
        [FromQuery] string? status,
        [FromQuery] string? patientLastName)
        {
            var result = await _service.GetAppointments(status, patientLastName);
            return Ok(result);
        }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetAppointment(int id)
    {
        var result = await _service.GetAppointment(id);
        
        if (result == null)
            return NotFound(new ErrorResponseDto { Message = "Not found" });

        return Ok(result);
    }
}