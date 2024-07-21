import { expect, describe, it } from "vitest";

import * as utility from "../utility";

describe("winPath", () => {
	it("should convert unix paths to windows paths", () => {
		expect(utility.winPath("foo/bar/baz")).toBe("foo\\bar\\baz");
	});

	it("should keep windows paths unchanged", () => {
		expect(utility.winPath("foo\\bar\\baz")).toBe("foo\\bar\\baz");
	});
});
