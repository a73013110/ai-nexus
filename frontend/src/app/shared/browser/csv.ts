// Excel formula prefixes must remain plain text when exported administrative data is opened.
export function toCsv(rows: readonly (readonly unknown[])[]): string {
  return (
    '\ufeff' +
    rows
      .map((row) =>
        row
          .map((value) => {
            let text = String(value ?? '');
            if (/^[\s]*[=+@-]/.test(text)) text = "'" + text;
            return '"' + text.replaceAll('"', '""') + '"';
          })
          .join(','),
      )
      .join('\r\n')
  );
}
