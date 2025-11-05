# Simple Ignition Sample

This sample demonstrates the basic usage of the Veggerby.Ignition library in a console application.

## What it demonstrates

- Basic signal registration and coordination
- Simple logging integration
- Basic success/failure handling
- Per-signal timeout configuration

## Signals included

1. **DatabaseConnectionSignal** - Simulates establishing a database connection (1s delay, 5s timeout)
2. **ConfigurationLoadSignal** - Simulates loading application configuration (0.5s delay, 3s timeout)

## Running the sample

```bash
cd samples/Simple
dotnet run
```

## Expected output

```text
info: Simple.Program[0]
      Starting application initialization...
info: Simple.DatabaseConnectionSignal[0]
      Establishing database connection...
info: Simple.ConfigurationLoadSignal[0]
      Loading application configuration...
info: Simple.ConfigurationLoadSignal[0]
      Configuration loaded successfully
info: Simple.DatabaseConnectionSignal[0]
      Database connection established successfully
info: Simple.Program[0]
      Initialization completed in 1000ms
info: Simple.Program[0]
      All initialization signals completed successfully!
info: Simple.Program[0]
      Signal 'database-connection': Succeeded (1000ms)
info: Simple.Program[0]
      Signal 'configuration-load': Succeeded (500ms)
info: Simple.Program[0]
      Application is ready to serve requests.
```

## Key concepts

- **IIgnitionSignal**: Custom signals implement this interface
- **IIgnitionCoordinator**: Manages and coordinates all signals
- **IgnitionResult**: Contains the aggregated results of all signals
- **Default behavior**: Parallel execution with best-effort policy
