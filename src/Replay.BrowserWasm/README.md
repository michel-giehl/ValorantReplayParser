# Replay.BrowserWasm

This project packages the VALORANT replay parser as a .NET 10 browser-WASM
bundle. Parsing runs in a Web Worker and replay bytes remain in the browser.

Install the required workloads once:

```powershell
dotnet workload install wasm-tools wasm-experimental
```

Publish the deployable static assets:

```powershell
dotnet publish "src\Replay.BrowserWasm\Replay.BrowserWasm.csproj" -c Release
```

Copy the contents of `bin\Release\net10.0\publish\wwwroot` to a directory
served by the viewer. Preserve the `_framework` directory beside
`replay-parser-worker.js`.

Use the client from the viewer:

```javascript
import { ValorantReplayParserWorker } from "./replay-parser-client.js";

const parser = new ValorantReplayParserWorker();
const result = await parser.parse(file);
```

`parse` accepts a `File`, `Blob`, or `ArrayBuffer` and returns the parsed JSON
object. Passing an `ArrayBuffer` transfers ownership to the worker and detaches
the caller's buffer.
