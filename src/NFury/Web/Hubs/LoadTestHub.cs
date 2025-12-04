using Microsoft.AspNetCore.SignalR;

namespace NFury.Web.Hubs;

/// <summary>
/// SignalR hub for real-time load test communication.
/// Handles client connections and group management for test progress updates.
/// </summary>
public class LoadTestHub : Hub
{
    /// <summary>
    /// Called when a new client connects to the hub.
    /// Sends a confirmation message with the connection ID.
    /// </summary>
    /// <returns>A task representing the asynchronous operation</returns>
    public override async Task OnConnectedAsync()
    {
        await Clients.Caller.SendAsync("Connected", new SignalRConnectedMessage { ConnectionId = Context.ConnectionId });
        await base.OnConnectedAsync();
    }

    /// <summary>
    /// Called when a client disconnects from the hub.
    /// </summary>
    /// <param name="exception">The exception that caused the disconnection, if any</param>
    /// <returns>A task representing the asynchronous operation</returns>
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Adds the calling client to a test-specific group to receive updates for that test.
    /// </summary>
    /// <param name="testId">The unique identifier of the test to join</param>
    /// <returns>A task representing the asynchronous operation</returns>
    public async Task JoinTestGroup(string testId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, testId);
    }

    /// <summary>
    /// Removes the calling client from a test-specific group.
    /// </summary>
    /// <param name="testId">The unique identifier of the test to leave</param>
    /// <returns>A task representing the asynchronous operation</returns>
    public async Task LeaveTestGroup(string testId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, testId);
    }
}
