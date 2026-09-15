using Microsoft.Data.Sqlite;

namespace Messaging;

public sealed record SendMessage(string RecipientId, string Body, string ClientMessageId);
public sealed record StoredMessage(long Sequence, string Id, string ClientMessageId,
    string SenderId, string RecipientId, string Body, DateTimeOffset SentAt);
public sealed record SendResult(StoredMessage Message, bool Replayed);
public sealed record MessagePage(IReadOnlyList<StoredMessage> Messages, long NextCursor);
public sealed class IdempotencyConflictException : Exception;

/// <summary>
/// A durable local store. SQLite serializes writers; uniqueness is enforced by
/// the database, not a process-local cache. Each operation owns its connection.
/// </summary>
public sealed class MessageStore(string connectionString, TimeProvider clock)
{
    public void Initialize()
    {
        using var db = Open();
        using var command = db.CreateCommand();
        command.CommandText = """
            PRAGMA journal_mode=WAL;
            CREATE TABLE IF NOT EXISTS messages (
              sequence INTEGER PRIMARY KEY AUTOINCREMENT,
              id TEXT NOT NULL,
              tenant TEXT NOT NULL,
              sender TEXT NOT NULL,
              recipient TEXT NOT NULL,
              client_id TEXT NOT NULL,
              body TEXT NOT NULL,
              sent_at TEXT NOT NULL,
              UNIQUE(tenant, sender, client_id)
            );
            CREATE INDEX IF NOT EXISTS ix_messages_sender
              ON messages(tenant, sender, recipient, sequence);
            CREATE INDEX IF NOT EXISTS ix_messages_recipient
              ON messages(tenant, recipient, sender, sequence);
            """;
        command.ExecuteNonQuery();
    }

    // Read-only probe: validate the schema without creating a missing database.
    public void CheckReadiness()
    {
        var settings = new SqliteConnectionStringBuilder(connectionString)
        {
            Mode = SqliteOpenMode.ReadOnly,
            DefaultTimeout = 1
        };
        using var db = new SqliteConnection(settings.ToString());
        db.Open();
        using var command = db.CreateCommand();
        command.CommandText = "SELECT sequence,id,tenant,sender,recipient,client_id,body,sent_at FROM messages LIMIT 0";
        command.ExecuteNonQuery();
    }

    public SendResult Send(string tenant, string sender, SendMessage input)
    {
        using var db = Open();
        using var transaction = db.BeginTransaction();
        using var insert = db.CreateCommand();
        insert.Transaction = transaction;
        insert.CommandText = """
            INSERT INTO messages(id,tenant,sender,recipient,client_id,body,sent_at)
            VALUES($id,$tenant,$sender,$recipient,$client,$body,$at)
            ON CONFLICT(tenant,sender,client_id) DO NOTHING;
            """;
        insert.Parameters.AddWithValue("$id", Guid.NewGuid().ToString("N"));
        insert.Parameters.AddWithValue("$tenant", tenant);
        insert.Parameters.AddWithValue("$sender", sender);
        insert.Parameters.AddWithValue("$recipient", input.RecipientId);
        insert.Parameters.AddWithValue("$client", input.ClientMessageId);
        insert.Parameters.AddWithValue("$body", input.Body);
        insert.Parameters.AddWithValue("$at", clock.GetUtcNow().ToString("O"));
        var created = insert.ExecuteNonQuery() == 1;

        using var find = db.CreateCommand();
        find.Transaction = transaction;
        find.CommandText = """
            SELECT sequence,id,client_id,sender,recipient,body,sent_at FROM messages
            WHERE tenant=$tenant AND sender=$sender AND client_id=$client;
            """;
        find.Parameters.AddWithValue("$tenant", tenant);
        find.Parameters.AddWithValue("$sender", sender);
        find.Parameters.AddWithValue("$client", input.ClientMessageId);
        StoredMessage message;
        using (var reader = find.ExecuteReader())
        {
            if (!reader.Read()) throw new InvalidOperationException("Missing inserted message");
            message = Read(reader);
        }
        if (message.RecipientId != input.RecipientId || message.Body != input.Body)
            throw new IdempotencyConflictException();
        transaction.Commit();
        return new SendResult(message, !created);
    }

    public MessagePage Conversation(string tenant, string user, string peer, long after, int limit)
    {
        using var db = Open();
        using var command = db.CreateCommand();
        command.CommandText = """
            SELECT sequence,id,client_id,sender,recipient,body,sent_at FROM messages
            WHERE tenant=$tenant AND sequence>$after
              AND ((sender=$user AND recipient=$peer) OR (sender=$peer AND recipient=$user))
            ORDER BY sequence LIMIT $limit;
            """;
        command.Parameters.AddWithValue("$tenant", tenant);
        command.Parameters.AddWithValue("$user", user);
        command.Parameters.AddWithValue("$peer", peer);
        command.Parameters.AddWithValue("$after", after);
        command.Parameters.AddWithValue("$limit", limit);
        var messages = new List<StoredMessage>();
        using var reader = command.ExecuteReader();
        while (reader.Read()) messages.Add(Read(reader));
        return new MessagePage(messages, messages.Count == 0 ? after : messages[^1].Sequence);
    }

    private SqliteConnection Open()
    {
        var db = new SqliteConnection(connectionString);
        db.Open();
        return db;
    }

    private static StoredMessage Read(SqliteDataReader row) => new(
        row.GetInt64(0), row.GetString(1), row.GetString(2), row.GetString(3),
        row.GetString(4), row.GetString(5), DateTimeOffset.Parse(row.GetString(6)));
}
