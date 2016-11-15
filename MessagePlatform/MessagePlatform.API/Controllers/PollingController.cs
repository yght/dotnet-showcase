using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using MessagePlatform.Core.Interfaces;
using MessagePlatform.API.DTOs;
using AutoMapper;

namespace MessagePlatform.API.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    public class PollingController : Controller
    {
        private readonly IMessageService _messageService;
        private readonly IUserConnectionService _connectionService;
        private readonly ILogger<PollingController> _logger;
        private readonly IMapper _mapper;

        public PollingController(
            IMessageService messageService,
            IUserConnectionService connectionService,
            ILogger<PollingController> logger,
            IMapper mapper)
        {
            _messageService = messageService;
            _connectionService = connectionService;
            _logger = logger;
            _mapper = mapper;
        }

        [HttpGet("messages")]
        public async Task<IActionResult> PollForMessages([FromQuery]DateTime? since = null)
        {
            var userId = User.Identity.Name;
            var timestamp = since ?? DateTime.UtcNow.AddMinutes(-5); // Default to last 5 minutes
            
            var messages = await _messageService.GetMessagesSinceAsync(userId, timestamp);
            var messageDtos = _mapper.Map<MessageDto[]>(messages);
            
            return Ok(new { 
                Messages = messageDtos,
                Timestamp = DateTime.UtcNow
            });
        }

        [HttpPost("heartbeat")]
        public async Task<IActionResult> Heartbeat()
        {
            var userId = User.Identity.Name;
            await _connectionService.UpdateLastSeenAsync(userId);
            
            return Ok(new { 
                Status = "alive",
                Timestamp = DateTime.UtcNow
            });
        }

        [HttpGet("status")]
        public async Task<IActionResult> GetConnectionStatus()
        {
            var userId = User.Identity.Name;
            var isOnline = await _connectionService.IsUserOnlineAsync(userId);
            var lastSeen = await _connectionService.GetLastSeenAsync(userId);
            
            return Ok(new { 
                IsOnline = isOnline,
                LastSeen = lastSeen
            });
        }
    }
}