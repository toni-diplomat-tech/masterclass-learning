// A form's text field, or "" when it is missing or holds a file.
export function textField(formData: FormData, name: string): string {
  const value = formData.get(name);
  return typeof value === "string" ? value : "";
}
