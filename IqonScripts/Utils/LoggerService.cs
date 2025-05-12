using System;
using Microsoft.Extensions.Logging;

namespace IqonScripts.Utils;

/// <summary>
/// Service for logging messages
/// </summary>
public class LoggerService
{
    private readonly ILogger _logger;
    private readonly bool _verbose;

    /// <summary>
    /// Initializes a new instance of the <see cref="LoggerService"/> class.
    /// </summary>
    /// <param name="logger">The logger instance</param>
    /// <param name="verbose">Whether to show verbose logs</param>
    public LoggerService(ILogger logger, bool verbose)
    {
        _logger = logger;
        _verbose = verbose;
    }

    /// <summary>
    /// Logs an information message
    /// </summary>
    /// <param name="message">The message to log</param>
    public void LogInformation(string message)
    {
        _logger.LogInformation(message);
        Console.ForegroundColor = ConsoleColor.White;
        Console.WriteLine($"INFO: {message}");
        Console.ResetColor();
    }
    
    /// <summary>
    /// Logs a verbose message if verbose logging is enabled
    /// </summary>
    /// <param name="message">The message to log</param>
    public void LogVerbose(string message)
    {
        if (!_verbose) return;
        
        _logger.LogDebug(message);
        Console.ForegroundColor = ConsoleColor.Gray;
        Console.WriteLine($"DEBUG: {message}");
        Console.ResetColor();
    }
    
    /// <summary>
    /// Logs a warning message
    /// </summary>
    /// <param name="message">The message to log</param>
    public void LogWarning(string message)
    {
        _logger.LogWarning(message);
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"WARNING: {message}");
        Console.ResetColor();
    }
    
    /// <summary>
    /// Logs an error message
    /// </summary>
    /// <param name="message">The message to log</param>
    /// <param name="exception">The exception that occurred</param>
    public void LogError(string message, Exception? exception = null)
    {
        if (exception != null)
        {
            _logger.LogError(exception, message);
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"ERROR: {message}");
            Console.WriteLine($"Exception: {exception.Message}");
            
            if (_verbose)
            {
                Console.WriteLine($"Stack trace: {exception.StackTrace}");
            }
            
            Console.ResetColor();
        }
        else
        {
            _logger.LogError(message);
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"ERROR: {message}");
            Console.ResetColor();
        }
    }
    
    /// <summary>
    /// Logs a success message
    /// </summary>
    /// <param name="message">The message to log</param>
    public void LogSuccess(string message)
    {
        _logger.LogInformation(message);
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"SUCCESS: {message}");
        Console.ResetColor();
    }
}
