import { App } from "antd";
import { errorText } from "@cafecr/ui";

export function useNotify() {
  const { message } = App.useApp();
  return {
    ok: (text: string) => message.success(text),
    fail: (error: unknown) => message.error(errorText(error)),
  };
}
