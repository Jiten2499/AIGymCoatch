export function monthBounds(month: string) {
  const [yearText, monthText] = month.split("-");
  const year = Number(yearText);
  const monthNumber = Number(monthText);
  const last = new Date(Date.UTC(year, monthNumber, 0)).getUTCDate();
  const pad = String(monthNumber).padStart(2, "0");
  return { from: `${yearText}-${pad}-01`, to: `${yearText}-${pad}-${String(last).padStart(2, "0")}` };
}

export function quarterBounds(month: string) {
  const startMonth = Math.floor((Number(month.slice(5, 7)) - 1) / 3) * 3 + 1;
  const year = month.slice(0, 4);
  const endMonth = startMonth + 2;
  const end = monthBounds(`${year}-${String(endMonth).padStart(2, "0")}`);
  return { from: `${year}-${String(startMonth).padStart(2, "0")}-01`, to: end.to };
}
