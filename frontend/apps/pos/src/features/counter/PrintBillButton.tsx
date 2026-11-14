import { useMutation } from "@tanstack/react-query";
import { Button, type ButtonProps } from "antd";
import { PrinterOutlined } from "@ant-design/icons";
import { api } from "../../api";
import { useNotify } from "../useNotify";
import { receiptFileName, saveBlob } from "./receipt";

/** "In bill": downloads the 80 mm receipt PDF of a bill (fetch with the auth header → blob → file). */
export function PrintBillButton({ billCode, ...props }: { billCode: string } & Omit<ButtonProps, "onClick" | "loading">) {
  const notify = useNotify();
  const print = useMutation({
    mutationFn: () => api.sessions.receiptPdf(billCode),
    onSuccess: (blob) => saveBlob(blob, receiptFileName(billCode)),
    onError: notify.fail,
  });
  return (
    <Button icon={<PrinterOutlined />} loading={print.isPending} onClick={() => print.mutate()} {...props}>
      In bill
    </Button>
  );
}
