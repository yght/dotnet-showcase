using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MessagePlatform.Core.Entities;
using MessagePlatform.Core.Interfaces;
using MessagePlatform.Infrastructure.Data.CosmosDb;
using Microsoft.AspNetCore.Mvc;

namespace MessagePlatform.API.Controllers
{
    [Route("api/[controller]")]
    public class DataDemoController : Controller
    {
        private readonly IRepository<User> _sqlUserRepository;
        private readonly CosmosDbRepository<User> _cosmosUserRepository;
        private readonly IRepository<Message> _sqlMessageRepository;
        private readonly CosmosDbRepository<Message> _cosmosMessageRepository;

        public DataDemoController(
            IRepository<User> sqlUserRepository,
            CosmosDbRepository<User> cosmosUserRepository,
            IRepository<Message> sqlMessageRepository,
            CosmosDbRepository<Message> cosmosMessageRepository)
        {
            _sqlUserRepository = sqlUserRepository;
            _cosmosUserRepository = cosmosUserRepository;
            _sqlMessageRepository = sqlMessageRepository;
            _cosmosMessageRepository = cosmosMessageRepository;
        }

        [HttpPost("sql/users")]
        public async Task<IActionResult> CreateUserInSql([FromBody] User user)
        {
            if (user == null)
                return BadRequest();

            user.Id = Guid.NewGuid().ToString();
            user.CreatedAt = DateTime.UtcNow;
            
            var result = await _sqlUserRepository.AddAsync(user);
            return Ok(result);
        }

        [HttpPost("cosmos/users")]
        public async Task<IActionResult> CreateUserInCosmos([FromBody] User user)
        {
            if (user == null)
                return BadRequest();

            user.Id = Guid.NewGuid().ToString();
            user.CreatedAt = DateTime.UtcNow;
            
            var result = await _cosmosUserRepository.AddAsync(user);
            return Ok(result);
        }

        [HttpGet("sql/users")]
        public async Task<IActionResult> GetSqlUsers()
        {
            var users = await _sqlUserRepository.GetAllAsync();
            return Ok(users);
        }

        [HttpGet("cosmos/users")]
        public async Task<IActionResult> GetCosmosUsers()
        {
            var users = await _cosmosUserRepository.GetAllAsync();
            return Ok(users);
        }

        [HttpPost("sql/messages")]
        public async Task<IActionResult> CreateMessageInSql([FromBody] Message message)
        {
            if (message == null)
                return BadRequest();

            message.Id = Guid.NewGuid().ToString();
            message.Timestamp = DateTime.UtcNow;
            
            var result = await _sqlMessageRepository.AddAsync(message);
            return Ok(result);
        }

        [HttpPost("cosmos/messages")]
        public async Task<IActionResult> CreateMessageInCosmos([FromBody] Message message)
        {
            if (message == null)
                return BadRequest();

            message.Id = Guid.NewGuid().ToString();
            message.Timestamp = DateTime.UtcNow;
            
            var result = await _cosmosMessageRepository.AddAsync(message);
            return Ok(result);
        }

        [HttpGet("sql/messages/{userId}")]
        public async Task<IActionResult> GetUserMessagesFromSql(string userId)
        {
            var messages = await _sqlMessageRepository.FindAsync(m => m.SenderId == userId || m.RecipientId == userId);
            return Ok(messages);
        }

        [HttpGet("cosmos/messages/{userId}")]
        public async Task<IActionResult> GetUserMessagesFromCosmos(string userId)
        {
            var messages = await _cosmosMessageRepository.FindAsync(m => m.SenderId == userId || m.RecipientId == userId);
            return Ok(messages);
        }

        [HttpPut("sql/users/{id}")]
        public async Task<IActionResult> UpdateUserInSql(string id, [FromBody] User user)
        {
            if (user == null || id != user.Id)
                return BadRequest();

            var exists = await _sqlUserRepository.ExistsAsync(id);
            if (!exists)
                return NotFound();

            await _sqlUserRepository.UpdateAsync(user);
            return NoContent();
        }

        [HttpPut("cosmos/users/{id}")]
        public async Task<IActionResult> UpdateUserInCosmos(string id, [FromBody] User user)
        {
            if (user == null || id != user.Id)
                return BadRequest();

            var exists = await _cosmosUserRepository.ExistsAsync(id);
            if (!exists)
                return NotFound();

            await _cosmosUserRepository.UpdateAsync(user);
            return NoContent();
        }

        [HttpDelete("sql/users/{id}")]
        public async Task<IActionResult> DeleteUserFromSql(string id)
        {
            await _sqlUserRepository.DeleteAsync(id);
            return NoContent();
        }

        [HttpDelete("cosmos/users/{id}")]
        public async Task<IActionResult> DeleteUserFromCosmos(string id)
        {
            await _cosmosUserRepository.DeleteAsync(id);
            return NoContent();
        }
    }
}