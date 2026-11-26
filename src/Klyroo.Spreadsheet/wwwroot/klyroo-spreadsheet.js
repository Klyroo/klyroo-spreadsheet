// Browser helpers of Klyroo.Spreadsheet.

/** Saves `text` as a file named `fileName`, as the browser's download does. */
export function downloadText(fileName, text, mimeType) {
  const url = URL.createObjectURL(new Blob([text], { type: mimeType }));
  const link = document.createElement('a');
  link.href = url;
  link.download = fileName;
  link.style.display = 'none';
  document.body.appendChild(link);
  link.click();
  link.remove();
  // Revoke after the click has been handled, or some browsers cancel the download.
  setTimeout(() => URL.revokeObjectURL(url), 0);
}
