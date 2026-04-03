const NAVIS_BASE_URL = process.env.NAVIS_BRIDGE_URL || "http://localhost:52120";
const REQUEST_TIMEOUT = 45000;

interface BridgeResponse {
  success: boolean;
  data: unknown;
  error?: string;
}

export async function callNavis(endpoint: string, body?: unknown): Promise<unknown> {
  const url = `${NAVIS_BASE_URL}${endpoint}`;
  const controller = new AbortController();
  const timeout = setTimeout(() => controller.abort(), REQUEST_TIMEOUT);

  try {
    const options: RequestInit = {
      method: body ? "POST" : "GET",
      headers: { "Content-Type": "application/json" },
      signal: controller.signal,
    };
    if (body) options.body = JSON.stringify(body);

    const res = await fetch(url, options);
    const json = (await res.json()) as BridgeResponse;
    if (!json.success) throw new Error(json.error || "Navisworks bridge returned an error.");
    return json.data;
  } catch (err: unknown) {
    if (err instanceof Error && err.name === "AbortError") {
      throw new Error("Request to Navisworks bridge timed out.");
    }
    if (err instanceof TypeError) {
      throw new Error("Cannot connect to Navisworks bridge. Start NavisworksBridge on localhost:52120.");
    }
    throw err;
  } finally {
    clearTimeout(timeout);
  }
}
