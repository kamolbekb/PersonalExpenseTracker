import { getInitData } from "../telegram/initData";

export async function api<T>(path: string, init: RequestInit = {}): Promise<T> {
	const res = await fetch(`/api${path}`, {
		...init,
		headers: {
			"Content-Type": "application/json",
			Authorization: `tma ${getInitData()}`,
			...(init.headers ?? {}),
		},
	});
	if (!res.ok) throw new Error(`API ${res.status}: ${await res.text()}`);
	if (res.status === 204) return undefined as T;
	return res.json() as Promise<T>;
}
