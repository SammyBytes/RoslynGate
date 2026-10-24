export interface Openable {
  open(): void;
}

export class Widget implements Openable {
  open(): void;
  open(doorId: string): void;
  open(doorId?: string): void {
    void doorId;
  }
}

export class SecureWidget extends Widget {
  override open(): void {}
}
