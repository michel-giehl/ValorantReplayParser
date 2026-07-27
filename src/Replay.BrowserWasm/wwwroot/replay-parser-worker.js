import { dotnet } from "./_framework/dotnet.js";

try {
    const { getAssemblyExports, getConfig, runMain } = await dotnet.create();
    const config = getConfig();
    const exports = await getAssemblyExports(config.mainAssemblyName);

    self.addEventListener("message", event => {
        const { requestId, buffer } = event.data ?? {};

        try {
            if (!(buffer instanceof ArrayBuffer)) {
                throw new TypeError("Replay input must be an ArrayBuffer.");
            }

            const json = exports.Replay.BrowserWasm.BrowserReplayParser.Parse(
                new Uint8Array(buffer));

            self.postMessage({ type: "result", requestId, json });
        } catch (error) {
            self.postMessage({
                type: "error",
                requestId,
                error: error instanceof Error ? error.message : String(error),
            });
        }
    });

    self.postMessage({ type: "ready" });
    await runMain();
} catch (error) {
    self.postMessage({
        type: "startup-error",
        error: error instanceof Error ? error.message : String(error),
    });
}
