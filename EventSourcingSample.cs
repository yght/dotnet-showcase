using System;
using System.Collections.Generic;

namespace EventSourcing
{
    public abstract class DomainEvent
    {
        public Guid Id { get; } = Guid.NewGuid();
        public DateTime OccurredAt { get; } = DateTime.UtcNow;
    }

    public class UserCreated : DomainEvent
    {
        public string UserId { get; set; }
        public string Email { get; set; }
        public string Name { get; set; }
    }

    public class UserEmailChanged : DomainEvent
    {
        public string UserId { get; set; }
        public string NewEmail { get; set; }
        public string OldEmail { get; set; }
    }

    public class EventStore
    {
        private readonly List<DomainEvent> _events = new();

        public void SaveEvent(DomainEvent domainEvent)
        {
            _events.Add(domainEvent);
        }

        public IEnumerable<DomainEvent> GetEvents(string aggregateId)
        {
            return _events;
        }
    }

    public class User
    {
        private readonly List<DomainEvent> _uncommittedEvents = new();
        
        public string Id { get; private set; }
        public string Email { get; private set; }
        public string Name { get; private set; }

        public static User Create(string id, string email, string name)
        {
            var user = new User();
            user.Apply(new UserCreated { UserId = id, Email = email, Name = name });
            return user;
        }

        public void ChangeEmail(string newEmail)
        {
            Apply(new UserEmailChanged { UserId = Id, NewEmail = newEmail, OldEmail = Email });
        }

        private void Apply(DomainEvent domainEvent)
        {
            When(domainEvent);
            _uncommittedEvents.Add(domainEvent);
        }

        private void When(DomainEvent domainEvent)
        {
            switch (domainEvent)
            {
                case UserCreated e:
                    Id = e.UserId;
                    Email = e.Email;
                    Name = e.Name;
                    break;
                case UserEmailChanged e:
                    Email = e.NewEmail;
                    break;
            }
        }

        public IEnumerable<DomainEvent> GetUncommittedEvents()
        {
            return _uncommittedEvents;
        }

        public void MarkEventsAsCommitted()
        {
            _uncommittedEvents.Clear();
        }
    }
}