/** "bill-QW12ER34.pdf", the same name the API sends in Content-Disposition. */
export function receiptFileName(billCode: string): string {
  return `bill-${billCode.replace(/[^A-Za-z0-9]/g, "")}.pdf`;
}

/** Saves a downloaded blob via a temporary object URL (the auth token stays in the fetch header). */
export function saveBlob(blob: Blob, fileName: string): void {
  const url = URL.createObjectURL(blob);
  const link = document.createElement("a");
  link.href = url;
  link.download = fileName;
  document.body.appendChild(link);
  link.click();
  link.remove();
  setTimeout(() => URL.revokeObjectURL(url), 30_000);
}
