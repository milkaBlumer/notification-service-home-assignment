# Notification delivery API

A small HTTP API that manages and delivers notifications across email, SMS, and push channels.

## Install

```
dotnet restore
```

## Run

```
dotnet run
```

Starts the HTTP server on port 3000.

NOTE: The server pre-populates in-memory storage with a few sample notifications on startup.

## Endpoints

### POST /notifications

Create a notification.

```
curl -X POST http://localhost:3000/notifications \
  -H "Content-Type: application/json" \
  -d '{"message":"Hello","targetChannels":[{"type":"email","value":"user@example.com"}]}'
```

### GET /notifications

List all notifications.

```
curl http://localhost:3000/notifications
```

### GET /notifications/:id

Fetch a single notification by id. Returns 404 if not found.

```
curl http://localhost:3000/notifications/1
```

### PUT /notifications/:id

Update a notification. Returns 404 if not found.

```
curl -X PUT http://localhost:3000/notifications/1 \
  -H "Content-Type: application/json" \
  -d '{"message":"Updated message"}'
```

### POST /notifications/:id/send

Send a single notification. Returns 404 if not found.

```
curl -X POST http://localhost:3000/notifications/1/send
```

### POST /notifications/send-bulk

Send all pending notifications.

```
curl -X POST http://localhost:3000/notifications/send-bulk
```

## Changes made in this branch

These are the code changes included in this branch:

- Updated `Program.cs` to improve request validation and HTTP response handling.
  - `POST /notifications` now validates payload structure, channel type/value, and message presence.
  - `PUT /notifications/:id` now validates updates, rejects empty or invalid payloads, and returns `404` for missing notifications.
  - `POST /notifications` now returns `201 Created` with the newly created resource location.
  - All relevant routes return proper status codes: `200 OK`, `400 Bad Request`, `404 Not Found`, and `201 Created`.
  - Added helper validation logic in `Program.cs` for channel type checking and structured validation error messages.
- Added integration tests in a new test project `NotificationApi.Tests`:
  - `NotificationApi.Tests\NotificationApiTests.cs` covers invalid payloads, invalid channels, successful creation, missing updates, and `404 Not Found` behavior.

## Testing

Run project tests with:

```
dotnet test NotificationApi.Tests\NotificationApi.Tests.csproj --no-restore
```
