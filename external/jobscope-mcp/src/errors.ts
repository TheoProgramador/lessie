export class JobscopeError extends Error {
  readonly jobscopeKind: true = true;
  constructor(message?: string) {
    super(message);
    this.name = new.target.name;
  }
}
export class NotFoundError extends JobscopeError {}
export class RateLimitError extends JobscopeError {}
export class NetworkError extends JobscopeError {}
export class ParseError extends JobscopeError {}

export function isKnownError(e: unknown): e is JobscopeError {
  return e instanceof JobscopeError;
}
