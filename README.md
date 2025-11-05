# Foundry Local for Windows Server

## Overview

The following sample demonstrates how **Windows Server** can be used to run AI workloads on-premises with **Foundry Local**, ensuring data privacy and compliance with the strict requirements of regulated industries.

The **ContosoMedical** application highlights two AI-driven scenarios:

- **Medical Record Summarization**: Automatically condenses lengthy patient reports into concise medical summaries.
- **Medical Record Translation**: Translates medical documents from foreign languages into English while preserving medical terminology and formatting.

## Setup
### Installing Foundry Local on Windows Server 2025
1. **Download Foundry Local**
    ```bash
    winget install Microsoft.FoundryLocal
    ```

2. **Start the Foundry Local service**
    ```bash
    foundry service start
    ```
3. **Download a Language Model**

    For example, to download `phi-4-mini`:
    ```bash
    foundry model download phi-4-mini
    ```
For additional details on the Foundry Local CLI and the list of available language models, see [Foundry Local documentation](https://learn.microsoft.com/en-us/azure/ai-foundry/foundry-local/).

### Accessing the Foundry Local service over the network
By default,  Foundry Local listens on `127.0.0.1:<foundry-local-port>`, which restricts inference requests to the local machine.

To enable access from other devices on the network (or connected via VPN), use Windows PortProxy to forward external traffic on port `9000` to the Foundry Local service port.

1. **Create a port proxy**
    ```bash
    netsh interface portproxy add v4tov4 listenport=9000 listenaddress=0.0.0.0 connectport=<foundry-local-port> connectaddress=127.0.0.1
    ```

2. **Allow inbound TCP traffic**
    ```bash
    netsh advfirewall firewall add rule name="Allow Port 9000 Inbound" dir=in action=allow protocol=TCP localport=9000
    ```

3. **Verify connectivity**
    
    From any host on the same network, confirm that the Foundry Local service is reachable:
    ```bash
    curl http://<server-ip>:9000/openai/status
    ```

### How to use ContosoMedical app

#### Prerequisites
   - .NET Framework 4.8 or later
   - Visual Studio 2019 or later

#### Run the application
Open the solution in Visual Studio and build the project.
Run the application by pressing the F5 key or by clicking on the Start button in the toolbar.

## Architecture

### System Overview

The sample application uses a client–server architecture, where the WPF desktop client processes medical records by leveraging Language Model capabilities hosted on Windows Server instances configured as described above.

```
┌──────────────────────────────────────────┐
│             ContosoMedical               │
│  ┌─────────────────────────────────────┐ │
│  │           WPF Frontend              │ │
│  │   ┌─────────────────────────────┐   │ │
│  │   │ Patient Records Interface   │   │ │
│  │   │                             │   │ │
│  │   └─────────────────────────────┘   │ │
│  └─────────────────────────────────────┘ │
│  ┌─────────────────────────────────────┐ │
│  │              Services               │ │
│  │   ┌─────────────┐ ┌─────────────┐   │ │
│  │   │ Summarizer  │ │ Translator  │   │ │
│  │   │ (Map-Reduce)│ │ (Chunking)  │   │ │
│  │   └─────────────┘ └─────────────┘   │ │
│  └─────────────────────────────────────┘ │
└──────────────────────────────────────────┘
                │ HTTP/REST
                │ (OpenAI-Compatible API)
┌──────────────────────────────────────────┐
│           Windows Server Layer           │ 
│  ┌─────────────────────────────────────┐ │
│  │           Foundry Local             │ │
│  │   ┌─────────────┐ ┌─────────────┐   │ │
│  │   │   Phi-3.5   │ │   Phi-4     │   │ │
│  │   │    Mini     │ │    Mini     │   │ │
│  │   │             │ │             │   │ │
│  │   └─────────────┘ └─────────────┘   │ │
│  └─────────────────────────────────────┘ │
└──────────────────────────────────────────┘
```

### Main Components

```
ContosoMedical/
├── Services/
│   ├── Summarizer.cs          # Map-Reduce summarization
│   ├── Translator.cs          # Chunk-based translation
│   └── ...
├── Models/
│   ├── Patient.cs             # Patient data structures
│   └── ...
├── DefaultDataAssets/         # Synthetic patient data
└── App.config                 # Foundry Local endpoint configuration
```

## Foundry Local Integration

### Connection Configuration

The application connects to Foundry Local endpoints defined in `App.config`:

```xml
<appSettings>
  <add key="FoundryLocalEndPoint1" value="http://10.137.212.105:9000" />
  <add key="FoundryLocalEndPoint2" value="http://10.137.214.85:9000" />
  <add key="FoundryLocalLanguageModel" value="Phi-3.5-mini-instruct-generic-cpu:1" />
  <add key="FoundryLocalLanguageModel2" value="Phi-4-mini-instruct-generic-cpu:4" />
</appSettings>
```

### HTTP Client Integration
The application’s `Summarizer.cs` and `Translator.cs` services use `HttpClient` to communicate with Foundry Local:

```csharp
// Initialize HTTP clients for Foundry Local communication
var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(300) };
var endpoint = configurationManager.GetAppSetting("FoundryLocalEndPoint1");
```

### OpenAI-Compatible API Usage
Each request to Foundry Local follows the standard OpenAI chat completions schema:

```csharp
var requestBody = new
{
    model = configurationManager.GetAppSetting("FoundryLocalLanguageModel"),
    messages = new[]
    {
        new { role = "system", content = "You are a medical summarization assistant..." },
        new { role = "user", content = "Summarize this medical record section..." }
    },
    max_tokens = 200,
    temperature = 0.0
};

// Send to Foundry Local endpoint
var response = await httpClient.PostAsync(endpoint + "/v1/chat/completions", content);
```

## Data generation

This application uses the [Synthea](https://github.com/synthetichealth/synthea) synthetic patient data generator to create realistic medical records for testing and demonstration purposes. Synthea is an open-source synthetic patient generator that models the medical history of synthetic patients.

### Generating Synthetic Patient Data with Synthea

1. **Prerequisites**
   - Java 11 or later (required to run Synthea)

2. **Download Synthea**
   
   Download the latest pre-built JAR file from the [Synthea releases page](https://github.com/synthetichealth/synthea/releases):
   ```bash
   curl -L -O https://github.com/synthetichealth/synthea/releases/download/master-branch-latest/synthea-with-dependencies.jar
   ```

3. **Generate Patient Data**
   
   The ContosoMedical application expects patient data in plain text format.
   
   To generate a single patient record:
   ```bash
   java -jar synthea-with-dependencies.jar --exporter.text.export true -generate.append_numbers_to_person_names false
   ```
   
   To generate multiple patient records (e.g., 10 patients):
   ```bash
   java -jar synthea-with-dependencies.jar -p 10 --exporter.text.export true -generate.append_numbers_to_person_names false
   ```
   
   **Parameters explained:**
   - `--exporter.text.export true` - Enables plain text format export
   - `-generate.append_numbers_to_person_names false` - Prevents numeric suffixes from being added to patient names

4. **Locate Generated Data**
   
   By default, Synthea outputs patient records in the `output` directory in various formats including FHIR, C-CDA, and plain text.

For more detailed instructions and advanced configuration options, see the [Synthea Basic Setup and Running guide](https://github.com/synthetichealth/synthea/wiki/Basic-Setup-and-Running).

## Data pre-processing

Both summarization and translation workflows begin with a similar preprocessing stage.
The application first identifies medical record sections based on a known delimiter, and then divides each section into manageable chunks while preserving natural text boundaries (e.g., line breaks).

This chunking strategy ensures that related medical information stays together and that each model request fits within language model input limits.
Chunk size varies depending on the operation:

- **Summarization**: ~3,000 characters per chunk for efficient content compression.
- **Translation**: ~500 characters per chunk (450 for medications) for precise handling of specialized terminology.

## Model Selection
The application uses different language models optimized for specific tasks:
- **Phi-3.5-mini-instruct**: Primary model for summarization and general translation
- **Phi-4-mini-instruct**: Specialized for medication section translation (better pharmaceutical terminology handling)


## Summarization for Long Text Inputs

The ContosoMedical application addresses the challenge of summarizing lengthy medical reports that can span hundreds of lines and exceed the input limits of small language models. These records often include detailed patient histories, multiple clinical sections, and extensive observations that make single-pass summarization impractical.

To overcome this, the summarization process uses a **Map-Reduce** approach designed for medical content. In the **Map phase**, individual chunks are processed in parallel across available Foundry Local endpoints, with each chunk receiving a summarization prompt that generates a 200-token summary that preserves the critical medical information. Multiple workers simultaneously process different chunks, automatically load-balancing the work across available server endpoints.

The **Reduce phase** takes all the section summaries and combines them into a single, coherent patient overview. This final integration step uses specialized prompts that emphasize the most important medical data including diagnoses, treatments, medications, and clinical results, while eliminating redundancy and maintaining a consistent medical narrative. 

**Map Phase Prompt** (for intermediate summaries):
```
System: "You are a precise summarization assistant of a patient's record. You'll be presented with one or more sections of a patient's medical record."

User: "Generate a concise summary of the following medical record section(s), prioritizing the most recent information. DO NOT exceed 200 tokens. DO NOT include the token count in the summary

Text:
[SECTION CONTENT]"
```

**Reduce Phase Prompt** (for final summary):
```
System: "You are an expert summarizer that merges multiple summaries into one cohesive overview in at most 300 words."

User: "You will be given multiple summaries of a medical report. Generate one final concise summary with emphasis on medical data which include details in the following context: (Patient details, allergies, medication, conditions, procedures, treatments, doctor or provider visits and clinical results). DO NOT exceed 300 words in generating the final summary.

SUMMARIES:
[INTERMEDIATE SUMMARIES]"
```

## Translation for Long Text Inputs
In addition to summarization, the application also supports translation of patient reports into English. While translation shares some of the challenges of summarization, it presents unique challenges as it must avoid information loss, maintain clinical accuracy, and handle specialized terminology.

Therefore, the translation process employs a simpler parallel processing algorithm that focuses on maintaining document structure and medical accuracy. The algorithm processes chunks across available Foundry Local endpoints, with each chunk receiving a translation prompt designed to preserve medical formatting and terminology.

After translation, all chunks are reassembled in their original order, producing a complete, structured, and clinically accurate English version of the medical record.

**Translation Prompt** (for all chunks):
```
System: "You are a professional medical translator. If a label (e.g., [ATTUALE], [INTERROTTO]) appears, translate it literally (e.g., [CURRENT], [STOPPED])."

User: "Translate the following medical data about a patient from {source_language} into English.
Preserve the structure and formatting of the original text as much as possible.
Do not add any translator explanations, or notes, or commentary. The only output should be the translated text.

Text:
[CHUNK CONTENT]"
```

## Limitations
The current implementation of Foundry Local and the ContosoMedical sample operates under the following limitations:

- **Private Preview**: Foundry Local is currently in Private Preview and may be subject to feature changes, limited availability, or temporary instability.
- **Model Availability**: Not all language models are available in the Private Preview. The specific model required for your scenario may not yet be supported.
- **No Embedding Model Support**: Embedding models are not yet supported. Features such as semantic search, document retrieval, or similarity-based indexing are unavailable in this release.
- **No Concurrency Support**: Concurrent inference requests to Foundry Local are not yet supported. Requests are processed sequentially, and parallel execution across multiple endpoints must be managed at the application level.

## Future Work
As Foundry Local continues to evolve, future updates to these samples will explore additional capabilities on Windows Server, including agentic workflows, containerization, advanced model integrations, and performance improvements.

Community contributions and feedback are highly encouraged and greatly appreciated.

## Trademarks

This project may contain trademarks or logos for projects, products, or services. Authorized use of Microsoft 
trademarks or logos is subject to and must follow 
[Microsoft's Trademark & Brand Guidelines](https://www.microsoft.com/en-us/legal/intellectualproperty/trademarks/usage/general).
Use of Microsoft trademarks or logos in modified versions of this project must not cause confusion or imply Microsoft sponsorship.
Any use of third-party trademarks or logos are subject to those third-party's policies.