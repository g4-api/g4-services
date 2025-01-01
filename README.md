# G4 Hub API

[![Build, Test & Release G4™ API](https://github.com/g4-api/g4-services/actions/workflows/release-pipline.yml/badge.svg)](https://github.com/g4-api/g4-services/actions/workflows/release-pipline.yml)
![Docker Image Version](https://img.shields.io/docker/v/g4api/g4-hub?style=flat&logo=docker&logoColor=959da5&label=Docker%20Version&labelColor=24292f)

The **G4 Hub API** provides a comprehensive interface for managing templates, environments, integration metadata, and automation workflows within the G4™ Engine. This repository contains the source code and configuration necessary to deploy and interact with the API service.

---

## Table of Contents

- [Features](#features)
- [Getting Started](#getting-started)
  - [Prerequisites](#prerequisites)
  - [Installation](#installation)
    - [Option 1: Run with Docker](#option-1-run-with-docker)
    - [Option 2: Run Standalone](#option-2-run-standalone)
- [Usage](#usage)
  - [Invoking Automation Workflows](#invoking-automation-workflows)
- [API Documentation](#api-documentation)
- [Configuration](#configuration)
- [Contributing](#contributing)
- [License](#license)
- [Support](#support)
- [Acknowledgements](#acknowledgements)

---

## Features

- **Template Management**: Add, update, retrieve, and delete templates efficiently.
- **Environment Operations**: Create, modify, and remove environments and their associated parameters.
- **Automation Invocation**: Invoke and initialize automation workflows seamlessly.
- **Integration Metadata Access**: Retrieve plugin manifests, caches, and synchronize integration data.

---

## Getting Started

### Prerequisites

- **.NET 8.0 SDK or higher**: Required if running the service standalone.
- **Docker**: Required if running the service using Docker (version 20.10 or higher).
- **API Client**: Use tools like `curl`, Postman, or any HTTP client library to interact with the API.

### Installation

#### Option 1: Run with Docker

> :information_source: Important
>  
> You can run the service using Docker directly from docker hub by running the following command:
>  
> `docker run -d -p 9944:9944 g4api/g4-hub:latest`

1. **Clone the Repository**

   ```bash
   git clone https://github.com/g4-api/g4-services.git
   cd g4-services-hub
   ```

2. **Build the Docker Image**

   ```bash
   docker build -f ./docker/G4.Services.Hub.Dockerfile -t g4-services-hub .
   ```

3. **Run the Docker Container**

   ```bash
   docker run -d -p 9944:9944 --name g4-services-hub g4-services-hub
   ```

4. **Access the API**

   - The API will be accessible at `http://localhost:9944/swagger`.

#### Option 2: Run Standalone

1. **Install .NET SDK**

   - Download and install the [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) suitable for your operating system (Windows, Linux, or macOS).

2. **Download the Latest Release**

   - Go to the [Releases](https://github.com/g4-api/g4-services/releases) page.
   - Download the latest production version ZIP file.

3. **Extract the Package**

   - Extract the contents of the ZIP file to your desired location.

4. **Run the Service**

   ```bash
   dotnet G4.Services.Hub.dll
   ```

5. **Access the API**

   - The API will be accessible at `http://localhost:9944/swagger`.

---

## Usage

### Invoking Automation Workflows

The API allows you to invoke automation sessions using the `/automation/invoke` endpoint. Below is an example of how to structure the request body for invoking an automation workflow.

#### Endpoint

```http
POST /api/v4/g4/automation/invoke
```

#### Example Request Body

```json
{
  "authentication": {
    "username": "{{ApiUser}}"
  },
  "driverParameters": {
    "driver": "MicrosoftEdgeDriver",
    "driverBinaries": "{{DriverBinaries}}",
    "capabilities": {
      "alwaysMatch": {
        "browserName": "MicrosoftEdge"
      },
      "firstMatch": [{}]
    }
  },
  "stages": [
    {
      "name": "Sample Automation Flow",
      "description": "Main stage for invoking Sample Automation Flow.",
      "jobs": [
        {
          "reference": {
            "name": "Sample Job",
            "description": "Sample Job."
          },
          "rules": [
            {
              "$type": "Action",
              "pluginName": "GoToUrl",
              "argument": "about:blank",
              "regularExpression": "(?s).*"
            },
            {
              "$type": "Action",
              "pluginName": "WriteLog",
              "argument": "The first 8 alphanumeric characters of the GUID are {{$New-Guid --Pattern:^\\w{8}}}"
            },
            {
              "$type": "Action",
              "pluginName": "CloseBrowser"
            }
          ],
          "stopOnError": false
        }
      ]
    }
  ]
}
```

*Note: Ensure that you replace placeholders like `<your-username>`, `{{ApiUser}}`, and `{{DriverBinaries}}` with actual values relevant to your setup.*

#### Description

- **authentication**: Contains credentials for API access. Replace `{{ApiUser}}` with your API username/key/credentials.
- **driverParameters**: Configures the web driver for the automation session.
  - **driver**: Specifies the driver to use (e.g., `MicrosoftEdgeDriver`).
  - **driverBinaries**: Can be a physical location of the WebDriver binaries or a Selenium Grid endpoint. Replace `{{DriverBinaries}}` with the appropriate file path or grid URL.
  - **capabilities**: Defines the desired capabilities for the driver session.
- **stages**: Defines the stages of the automation process.
  - **name**: Name of the stage.
  - **description**: A brief description of the stage.
  - **jobs**: A list of jobs to execute within the stage.
    - **reference**: Metadata about the job.
      - **name**: Name of the job.
      - **description**: A brief description of the job's purpose.
    - **rules**: A sequence of actions to perform.
      - **$type**: The type of the rule (e.g., `Action`).
      - **pluginName**: The plugin to invoke.
      - **argument**: Arguments to pass to the plugin.
      - **regularExpression**: (Optional) A regex pattern for matching.
    - **stopOnError**: Indicates whether to halt the job if an error occurs.

#### Example Usage with `curl`

Save the example request body to a file named `request-body.json` and execute the following command:

```bash
curl -X POST http://localhost:9944/api/v4/g4/automation/invoke \
     -H "Content-Type: application/json" \
     -d @request-body.json
```

---

## API Documentation

Detailed API documentation is available via Swagger UI when the service is running. Access it at:

```
http://localhost:9944/swagger/index.html
```

## Contributing

Contributions are welcome! Please follow these steps:

1. **Fork the Repository**: Click the "Fork" button at the top right of the repository page.

2. **Create a Feature Branch**:

   ```bash
   git checkout -b feature/YourFeatureName
   ```

3. **Commit Your Changes**:

   ```bash
   git commit -am "Add new feature"
   ```

4. **Push to Your Fork**:

   ```bash
   git push origin feature/YourFeatureName
   ```

5. **Create a Pull Request**: Open a pull request against the `main` branch of this repository.

---

## License

This project is licensed under the [Apache v2.0](LICENSE).

---

## Support

If you encounter any issues or have questions, please open an issue in the repository or contact the maintainers.