/** Coalesces bursts without losing snapshots or delaying a terminal status. */
export class FramePublisher {
  private pending: number | null = null;
  private value = '';
  private published: string | null = null;
  constructor(
    private readonly publish: (value: string) => void,
    private readonly schedule: (callback: () => void) => number = (callback) =>
      window.setTimeout(callback, 32),
    private readonly cancel: (id: number) => void = (id) => window.clearTimeout(id),
  ) {}
  set(value: string) {
    this.value = value;
    if (this.pending === null)
      this.pending = this.schedule(() => {
        this.pending = null;
        this.flush();
      });
  }
  flush() {
    if (this.pending !== null) this.cancel(this.pending);
    this.pending = null;
    if (this.value !== this.published) {
      this.published = this.value;
      this.publish(this.value);
    }
  }
  dispose() {
    if (this.pending !== null) this.cancel(this.pending);
    this.pending = null;
  }
}
