using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using MessagePlatform.Core.Interfaces;
using MessagePlatform.Core.Entities;
using MessagePlatform.API.DTOs;
using AutoMapper;

namespace MessagePlatform.API.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    public class MessagesController : Controller
    {
        private readonly IMessageService _messageService;
        private readonly IUserConnectionService _connectionService;
        private readonly INotificationService _notificationService;
        private readonly ILogger<MessagesController> _logger;
        private readonly IMapper _mapper;

        public MessagesController(
            IMessageService messageService,
            IUserConnectionService connectionService,
            INotificationService notificationService,
            ILogger<MessagesController> logger,
            IMapper mapper)
        {
            _messageService = messageService;
            _connectionService = connectionService;
            _notificationService = notificationService;
            _logger = logger;
            _mapper = mapper;
        }

        [HttpPost]
        public async Task<IActionResult> SendMessage([FromBody]SendMessageDto messageDto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var senderId = User.Identity.Name; // Get from JWT claims
            var message = new Message
            {
                SenderId = senderId,
                RecipientId = messageDto.RecipientId,
                Content = messageDto.Content,
                Timestamp = DateTime.UtcNow,
                IsRead = false,
                ReplyToMessageId = messageDto.ReplyToMessageId
            };

            var savedMessage = await _messageService.SaveMessageAsync(message);
            var messageResponse = _mapper.Map<MessageDto>(savedMessage);

            // Send push notification instead of real-time SignalR
            await _notificationService.SendMessageNotificationAsync(messageDto.RecipientId, messageResponse);

            return Ok(messageResponse);
        }

        [HttpPost("group")]
        public async Task<IActionResult> SendGroupMessage([FromBody]SendGroupMessageDto messageDto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var senderId = User.Identity.Name;
            
            var message = new Message
            {
                SenderId = senderId,
                GroupId = messageDto.GroupId,
                Content = messageDto.Content,
                Timestamp = DateTime.UtcNow,
                IsRead = false
            };

            var savedMessage = await _messageService.SaveMessageAsync(message);
            var messageResponse = _mapper.Map<MessageDto>(savedMessage);

            // Send notifications to group members
            await _notificationService.SendGroupMessageNotificationAsync(messageDto.GroupId, messageResponse);

            return Ok(messageResponse);
        }

        [HttpGet("conversations/{userId}")]
        public async Task<IActionResult> GetConversation(string userId, [FromQuery]int page = 1, [FromQuery]int pageSize = 50)
        {
            var currentUserId = User.Identity.Name;
            
            if (pageSize > 100)
                pageSize = 100; // Limit page size to prevent performance issues
                
            var messages = await _messageService.GetConversationAsync(currentUserId, userId, page, pageSize);
            var messageDtos = _mapper.Map<MessageDto[]>(messages);
            
            return Ok(new { 
                Messages = messageDtos,
                Page = page,
                PageSize = pageSize,
                HasMore = messageDtos.Length == pageSize
            });
        }

        [HttpPut("{messageId}/read")]
        public async Task<IActionResult> MarkAsRead(string messageId)
        {
            var userId = User.Identity.Name;
            await _messageService.MarkAsReadAsync(messageId, userId);
            
            return Ok();
        }

        [HttpGet("unread")]
        public async Task<IActionResult> GetUnreadMessages()
        {
            var userId = User.Identity.Name;
            var messages = await _messageService.GetUnreadMessagesAsync(userId);
            var messageDtos = _mapper.Map<MessageDto[]>(messages);
            
            return Ok(messageDtos);
        }
    }
}