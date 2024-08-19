import { cpus } from 'node:os';
import path from "node:path";


export class HttpError extends Error {
	constructor(
		public code: number,
		message: string,
	) {
		super(message);
	}

	get codeMessage(): string {
		switch (this.code) {
			case 400:
				return "Bad Request";
			case 404:
				return "Not Found";
			default:
				return "Internal Server Error";
		}
	}
}

export function winPath(p: string): string {
	return p.split(path.posix.sep).join(path.win32.sep);
}

export function unixPath(p: string): string {
	return p.split(path.win32.sep).join(path.posix.sep);
}

/**
 * A lock that is granted when calling [[Semaphore.acquire]].
 */
type Lock = {
	release: () => void
}

/**
 * A task that has been scheduled with a [[Semaphore]] but not yet started.
 */
type WaitingPromise = {
	resolve: (lock: Lock) => void
	reject: (err?: Error) => void
}

/**
 * A [[Semaphore]] is a tool that is used to control concurrent access to a common resource. This implementation
 * is used to apply a max-parallelism threshold.
 */
export class Semaphore {
	private running = 0
	private waiting: WaitingPromise[] = []

	constructor(public max: number = cpus().length) {
		if (max < 1) {
			throw new Error(
				`Semaphore was created with a max value of ${max} but the max value cannot be less than 1`,
			)
		}
	}

	private take() {
		if (this.waiting.length > 0 && this.running < this.max) {
			this.running++

			// Get the next task from the queue
			const task = this.waiting.shift()!

			// Resolve the promise to allow it to start, provide a release function
			task.resolve({ release: this.release })
		}
	}

	public acquire(): Promise<Lock> {
		if (this.running < this.max) {
			this.running++
			return Promise.resolve({ release: this.release })
		}

		return new Promise<Lock>((resolve, reject) => {
			this.waiting.push({ resolve, reject })
		})
	}

	private release = () => {
		this.running--
		this.take()
	}

	/**
	 * Purge all waiting tasks from the [[Semaphore]]
	 */
	public purge() {
		this.waiting.forEach(task => {
			task.reject(
				new Error('The semaphore was purged and as a result this task has been cancelled'),
			)
		})

		this.running = 0
		this.waiting = []
	}
}