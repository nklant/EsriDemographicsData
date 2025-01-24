# Demographics Data Fetching Service

This project is a .NET Core application that fetches demographic data from an external API, processes it, and stores it in a SQL Server database. The application also caches the data to improve performance.

## Project Structure

- **DemographicsBackgroundService**: Contains the background service that fetches and processes the data.
- **DemographicsDb**: Contains the Entity Framework Core context and models for the database.
- **DemographicsWebApi**: Contains the web API for accessing the demographic data.

## Features

- **Background Service**: Periodically fetches data from an external API and updates the database.
- **Entity Framework Core**: Used for database operations.
- **Distributed Caching**: Caches the fetched data to improve performance.
- **Configuration**: Uses `appsettings.json` for configuration settings.
- **Dependency Injection**: Utilizes dependency injection for better modularity and testability.

## Design patterns used

- **Dependency Injection**: Used to inject dependencies like `IServiceProvider`, `IDistributedCache`, `IOptions<EndpointOptions>`, and `IOptions<CacheSettings>` into the `DataFetchingService` class.
- **Factory Pattern**: Implemented in the `DesignTimeDbContextFactory` class to create instances of `DemographicDbContext` at design time.
- **Singleton Pattern**: The `HttpClient` instance in the `DataFetchingService` class is effectively used as a singleton to ensure it is reused across multiple requests.
- **Repository Pattern (Data access via EF Core)**: The `DemographicDbContext` class acts as a repository for accessing and managing data in the database.
- **Builder Pattern**: Used in the `ConfigurationBuilder` to build the configuration settings from `appsettings.json`.

## Algorithms used
- **Hashing**: Used `SHA256` to compute a hash of the fetched data for comparison.
- **LINQ**: Used to group and aggregate data by `StateName`.

## Other techniques
- **Transaction Management**: Used `BeginTransactionAsync` to ensure atomicity when updating the database.
- **Background Service**: Implemented using the `IHostedService` interface in the `DataFetchingService` class to run background tasks.
- **HTTP Client**: Used to fetch data from an external API using `HttpClient`.
- **Entity Framework Core**: Used for database operations, including querying, adding, and removing data in the `DemographicDbContext`.
- **Distributed Caching**: Used `IDistributedCache` to cache the fetched data.
- **Configuration**: Used `IOptions` to access configuration settings from `appsettings.json`.
- **Design-Time DbContext Creation**: Implemented `IDesignTimeDbContextFactory` to provide the `DbContext` with the necessary configuration at design time.

## Getting Started

### Prerequisites

- .NET Core SDK
- SQL Server

### Installation

1. Clone the repository:
    ```shell
    git clone https://github.com/your-repo/demographics-data-fetching-service.git
    cd demographics-data-fetching-service
    ```

2. Install the required packages:
    ```shell
    dotnet restore
    ```

3. Update the connection string in `appsettings.json`:
    ```json
    {
      "ConnectionStrings": {
        "DefaultConnection": "Your SQL Server connection string here"
      }
    }
    ```

4. Apply the database migrations:
    ```shell
    dotnet ef database update --project DemographicsDb
    ```

### Running the Application

1. Start the application:
    ```shell
    dotnet run --project DemographicsWebApi
    ```

2. The API will be available at `https://localhost:5001`.

## Configuration

The application uses `appsettings.json` for configuration. Key settings include:

- **ConnectionStrings**: Database connection strings.
- **Endpoint**: Configuration for the external API endpoint.
- **CacheSettings**: Settings for the distributed cache.

## Design-Time DbContext Creation

The `DesignTimeDbContextFactory` class is used to provide the `DbContext` with the required configuration at design time. This is necessary for Entity Framework Core tools, such as migrations.