# Demographics Data Service

## Author
- Nikolay Antonov

This project is a .NET Core application that fetches demographic data from an external API, processes it, and stores it in a SQL Server database. The application also caches the data periodically to improve performance. It exposes a REST API, allowing clients to interact with the demographic data using the standard HTTP GET method.

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
- **Docker Support**: The application can be containerized for easy deployment using Docker. The `Dockerfile` is located in the solution root.

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
- **Logging**: Used `Serilog` to log information, warnings, and errors in `DemographicsWebApi\logs` folder.

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

3. Update the connection string to the database in `appsettings.json`:
    ```json
      "ConnectionStrings": {
        "DefaultConnection": "Connection string here"
      }
    ```

4. Update the connection string to the endpoint in `appsettings.json`:
    ```json
      "Endpoint": {
        "EndpointUri": "Endpoint string here"
      }
    ```

5. (Optional) Apply database migrations manually (app will do it at startup):
    ```shell
    dotnet ef database update --project DemographicsDb
    ```

### Database Schema

The application uses a SQL Server database to store demographic data. Below is a brief description of the schema:

### Tables

1. `DemographicsData`
    - Stores demographic information grouped by state.
    - Columns:
        - `Id` (int, PK): Id for each field
        - `StateName` (nvarchar(max), null): The name of the state
        - `Population` (int, null): The population of the state

2. `DataHash`
    - Stores a hash of the fetched data to detect changes and avoid redundant updates.
    - Columns:
        - `Id` (int, PK): Id for each field
        - `Hash` (nvarchar(max), not null): The hash value of the fetched data



### Running the Application

1. Start the application:
    ```shell
    dotnet run --project DemographicsWebApi
    ```

2. The API will be available at
    - `https://localhost:7067`
    - `http://localhost:5171`

### Running the application with Docker

The solution includes a `Dockerfile` located in the solution root, allowing you to easily containerize the Web API project. The Dockerfile includes all necessary dependencies to run the application.

### Building and running the container

1. Build the Docker image (from solution root):
    ```shell
    docker build -t demographics-service .
    ```
   
2. Run the Docker container:
    ```shell
    docker run -d -p 8080:80 --name demographics-container demographics-service
    ```
   
3. The API will be available at:
   - `http://localhost:8080`

## API Usage

### Endpoints

- **GET `/api/DemographicData`**:
    - Returns all demographic data (grouped by state name and population).

- **GET `/api/DemographicData?stateName=value`**:
    - Filters the results by the specified `stateName`.
    - Performs a case-insensitive search.
    - Matches exact or partial occurrences of the given `stateName`.
    - Example: `/api/DemographicData?stateName=new` will return all states with "new" (e.g., "New Mexico", ignoring case).

### Swagger UI

In development mode, there is access to Swagger UI at `https://localhost:7067/swagger`, which provides an interactive interface to test and document the available endpoints.

## Configuration

The application uses `appsettings.json` for configuration. Key settings include:

- **ConnectionStrings**: Database connection strings.
- **Endpoint**: Configuration for the external API endpoint.
- **CacheSettings**: Settings for the distributed cache (TTL, cacheKey).