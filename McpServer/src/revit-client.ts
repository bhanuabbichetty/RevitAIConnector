const REVIT_BASE_URL = process.env.REVIT_URL || "http://localhost:52010";
const REQUEST_TIMEOUT = 30000;

interface RevitResponse {
  Success: boolean;
  Data: unknown;
  Error?: string;
}

export async function callRevit(endpoint: string, body?: unknown): Promise<unknown> {
  const url = `${REVIT_BASE_URL}${endpoint}`;

  const controller = new AbortController();
  const timeout = setTimeout(() => controller.abort(), REQUEST_TIMEOUT);

  try {
    const options: RequestInit = {
      method: body ? "POST" : "GET",
      headers: { "Content-Type": "application/json" },
      signal: controller.signal,
    };

    if (body) {
      options.body = JSON.stringify(body);
    }

    const res = await fetch(url, options);
    const json = (await res.json()) as RevitResponse;

    if (!json.Success) {
      throw new Error(json.Error || "Revit returned an error.");
    }

    return json.Data;
  } catch (err: unknown) {
    if (err instanceof Error && err.name === "AbortError") {
      throw new Error("Request to Revit timed out. Is Revit running with the AI Connector add-in?");
    }
    if (err instanceof TypeError && (err as NodeJS.ErrnoException).cause) {
      throw new Error(
        "Cannot connect to Revit. Ensure Revit is running and the AI Connector add-in is loaded."
      );
    }
    throw err;
  } finally {
    clearTimeout(timeout);
  }
}
