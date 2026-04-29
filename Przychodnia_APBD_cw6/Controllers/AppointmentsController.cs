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

    [HttpGet("{idAppointment:int}")]
    public async Task<IActionResult> GetAppointment(int idAppointment)
    {
        var result = await _service.GetAppointment(idAppointment);
        
        if (result == null)
            return NotFound(new ErrorResponseDto { Message = "Appointment not found" });

        return Ok(result);
    }
    
    [HttpPost]
    public async Task<IActionResult> Create(CreateAppointmentRequestDto dto)
    {
        try
        {
            var id = await _service.CreateAppointmentAsync(dto);
            return Created($"/api/appointments/{id}", new { id });
        }
        catch (Exception ex)
        {
            if (ex.Message.Contains("already has appointment"))
                return Conflict(new ErrorResponseDto { Message = ex.Message });

            return BadRequest(new ErrorResponseDto { Message = ex.Message });
        }
    }
    
    [HttpPut("{idAppointment:int}")]
    public async Task<IActionResult> Update(int idAppointment, UpdateAppointmentRequestDto dto)
    {
        try
        {
            await _service.UpdateAppointmentAsync(idAppointment, dto);
            return Ok();
        }
        catch (Exception ex)
        {
            if (ex.Message.Contains("not found"))
                return NotFound(new ErrorResponseDto { Message = ex.Message });

            if (ex.Message.Contains("already has appointment"))
                return Conflict(new ErrorResponseDto { Message = ex.Message });

            return BadRequest(new ErrorResponseDto { Message = ex.Message });
        }
    }
    
    [HttpDelete("{idAppointment:int}")]
    public async Task<IActionResult> Delete(int idAppointment)
    {
        try
        {
            await _service.DeleteAppointmentAsync(idAppointment);
            return NoContent();
        }
        catch (Exception ex)
        {
            if (ex.Message.Contains("not found"))
                return NotFound(new ErrorResponseDto { Message = ex.Message });

            if (ex.Message.Contains("completed"))
                return Conflict(new ErrorResponseDto { Message = ex.Message });

            return BadRequest(new ErrorResponseDto { Message = ex.Message });
        }
    }
}