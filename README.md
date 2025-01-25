# Demographics Data Fetching Service

This project is a .NET Core application that fetches demographic data from an external API, processes it, and stores it in a SQL Server database. The application also caches the data periodically to improve performance. 

## Project Structure

- **DemographicsBackgroundService**: Contains the background service that fetches and processes the data.
- **DemographicsDb**: Contains the Entity Framework Core context and models for the database.
- **DemographicsLib**: Contains core business logic and interfaces for processing demographic data.
- **DemographicsWebApi**: Contains the web API for accessing the demographic data.

## Features

- **Background Service**: Periodically fetches data from an external API and updates the database. Implemented using the `IHostedService` interface.
- **Entity Framework Core (Code-First)**: Used for database operations and schema management. The database is automatically created or updated on application startup using migrations.
- **Distributed Caching**: Caches the fetched data to improve performance, currently using an in-memory provider (`AddDistributedMemoryCache`).
- **Configuration**: Uses `appsettings.json` for configuration settings and strongly typed options classes for `EndpointOptions` and `CacheSettings`.
- **Dependency Injection**: Utilizes DI for better modularity and testability, including services such as `DemographicDbContext`, `IDemographicDataService`, and others.
- **Separation of concerns**: Isolates business logic and service interfaces (`IDemographicDataService`) from the Web API and the data access layer. This improves maintainability and testability by allowing each layer to focus on a specific responsibility. This approach lets changing or reusing the business logic in other contexts (e.g., a desktop app or another service) without dragging in all the web and database code.

## Design patterns used

- **Dependency Injection**: Used to inject dependencies like `IServiceProvider`, `IDistributedCache`, `IOptions<EndpointOptions>`, and `IOptions<CacheSettings>` into the `DataFetchingService` class.
- **Factory Pattern**: Implemented in the `DesignTimeDbContextFactory` class to create instances of `DemographicDbContext` at design time (for EF Core tooling).
- **Singleton Pattern**: The `HttpClient` instance in the `DataFetchingService` class is effectively used as a singleton to ensure it is reused across multiple requests.
- **Repository Pattern (Data access via EF Core)**: The `DemographicDbContext` class acts as a repository for accessing and managing data in the database.
- **Builder Pattern**: Used in the `ConfigurationBuilder` to load config from JSON files, environment variables, etc.

## Algorithms used
- **Hashing**: Used `SHA256` to compute a hash of the fetched data for comparison.
- **LINQ**: Used to group and aggregate data by `StateName`.

## Other techniques
- **Transaction Management**: Uses `BeginTransactionAsync` ensures atomicity for database operations.
- **Background Service**: Implemented using the `IHostedService` interface in the `DataFetchingService` class to run background tasks.
- **HTTP Client**: Used to fetch data from an external API using `HttpClient`.
- **Entity Framework Core**: Used for database operations, including querying, adding, and removing data in the `DemographicDbContext`.
- **Distributed In-Memory Caching**: Used `IDistributedCache` to cache the fetched data.
- **Configuration**: Used `IOptions` to access configuration settings from `appsettings.json`.
- **Design-Time DbContext Creation**: Implemented `IDesignTimeDbContextFactory` to provide the `DbContext` with the necessary configuration at design time.

## Getting Started

### Prerequisites

- .NET Core SDK
- SQL Server

### Installation

1. Clone the repository:
    ```shell
    git clone https://github.com/nklant/EsriDemographicsData.git
    cd EsriDemographicsData
    ```

2. Install the required packages:
    ```shell
    dotnet restore
    ```

3. Update the connection string in `appsettings.json`:
    ```json
    {
      "ConnectionStrings": {
        "DefaultConnection": "Connection string here"
      }
    }
    ```

4. (Optional) Apply database migrations manually (app will do it at startup):
    ```shell
    dotnet ef database update --project DemographicDbContext
    ```

### Running the Application

1. Start the application:
    ```shell
    dotnet run --project DemographicsWebApi
    ```

2. The API will be available at `https://localhost:7067`.

## API Usage

### Endpoints

- **GET `/api/DemographicData`**: 
  - Returns all demographic data.

- **GET `/api/DemographicData?stateName=value`**: 
  - Filters the results by the specified `stateName`.
  - Performs a case-insensitive search.
  - Matches exact or partial occurrences of the given `stateName`.
  - Example: `/api/DemographicData?stateName=new` could return any states with "new" (e.g., "New Mexico", ignoring case).

### Swagger UI

In development mode, there is access to Swagger UI at `https://localhost:7067/swagger`, which provides an interactive interface to test and document the available endpoints.

## Configuration

The application uses `appsettings.json` for configuration. Key settings include:

- **ConnectionStrings**: Database connection strings.
- **Endpoint**: Configuration for the external API endpoint.
- **CacheSettings**: Settings for the distributed cache (TTL, cacheKey).

## Design-Time DbContext Creation

The `DesignTimeDbContextFactory` class is used to provide the `DbContext` with the required configuration at design time. This is necessary for Entity Framework Core tools, such as migrations.
