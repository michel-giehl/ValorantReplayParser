export class ValorantReplayParserWorker {
    #nextRequestId = 1;
    #pending = new Map();
    #worker;

    constructor(
        workerUrl = new URL("./replay-parser-worker.js", import.meta.url)) {
        this.#worker = new Worker(workerUrl, { type: "module" });
        this.ready = new Promise((resolve, reject) => {
            this.#worker.addEventListener("message", event => {
                if (event.data?.type === "ready") {
                    resolve();
                } else if (event.data?.type === "startup-error") {
                    reject(new Error(event.data.error));
                }
            });
        });

        this.#worker.addEventListener("message", event => {
            const { type, requestId, json, error } = event.data ?? {};
            if (type !== "result" && type !== "error") {
                return;
            }

            const request = this.#pending.get(requestId);
            if (!request) {
                return;
            }

            this.#pending.delete(requestId);
            if (type === "error") {
                request.reject(new Error(error));
            } else {
                request.resolve(JSON.parse(json));
            }
        });
    }

    async parse(fileOrBuffer) {
        await this.ready;

        const buffer = fileOrBuffer instanceof Blob
            ? await fileOrBuffer.arrayBuffer()
            : fileOrBuffer;
        if (!(buffer instanceof ArrayBuffer)) {
            throw new TypeError("Expected a File, Blob, or ArrayBuffer.");
        }

        const requestId = this.#nextRequestId++;
        const response = new Promise((resolve, reject) => {
            this.#pending.set(requestId, { resolve, reject });
        });

        this.#worker.postMessage({ requestId, buffer }, [buffer]);
        return response;
    }

    dispose() {
        this.#worker.terminate();
        for (const request of this.#pending.values()) {
            request.reject(new Error("Replay parser worker was disposed."));
        }

        this.#pending.clear();
    }
}
