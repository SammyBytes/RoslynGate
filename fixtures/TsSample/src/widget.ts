export interface Openable {
  open(): void;
}

export class Widget implements Openable {
  open(): void;
  open(doorId: string): void;
  open(doorId: number): void;
  open(doorId?: string | number): void {
    void doorId;
  }
}

export class SecureWidget extends Widget {
  override open(): void {}
}
