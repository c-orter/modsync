import { jsonc } from "jsonc";

export class JsonUtil {
	public deserializeJsonC(contents: string, _filename: string) {
		return jsonc.parse(contents.toString());
	}
}
