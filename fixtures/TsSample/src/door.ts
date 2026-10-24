import { Widget, SecureWidget } from "./widget.js";

export function useWidget(widget: Widget): void {
  widget.open();
  widget.open("main");
}

export const secure = new SecureWidget();
