"use client";

import { useMutation } from "@tanstack/react-query";
import { useState } from "react";
import { FormDialog } from "../../../components/FormDialog";
import { api, ApiError } from "../../../lib/api";
import { memberPaymentMethods } from "./memberWizard";

const referenceMethods = new Set<string>(["Upi", "DebitCard", "CreditCard", "BankTransfer", "OnlinePayment", "Cheque"]);

export function PtPackagePaymentDialog({
  currency,
  memberId,
  onCancel,
  onSaved,
  packageId,
  packageName,
  price,
  startDate,
  trainerId,
}: {
  currency: string;
  memberId: string;
  onCancel: () => void;
  onSaved: () => Promise<void>;
  packageId: string;
  packageName: string;
  price: number;
  startDate: string;
  trainerId: string | null;
}) {
  const [amount, setAmount] = useState("");
  const [method, setMethod] = useState<(typeof memberPaymentMethods)[number]>("Cash");
  const [payDate, setPayDate] = useState(new Date().toISOString().slice(0, 10));
  const [reference, setReference] = useState("");
  const [formError, setFormError] = useState<string | null>(null);
  const assign = useMutation({
    mutationFn: (payment: { amount: string; method: string; payDate: string; reference: string } | null) => api(`/members/${memberId}/pt-assignments`, {
      method: "POST",
      body: JSON.stringify({
        packageId,
        trainerId,
        startDate,
        paymentAmount: payment ? Number(payment.amount) : null,
        paymentMethod: payment ? payment.method : null,
        paymentDate: payment ? payment.payDate : null,
        paymentReferenceNumber: payment ? payment.reference.trim() || null : null,
        paymentNotes: null,
      }),
    }),
    onSuccess: onSaved,
  });

  function savePayment() {
    if (!amount.trim() || Number(amount) <= 0) {
      setFormError("Enter the amount received.");
      return;
    }
    if (Number(amount) > price) {
      setFormError("Payment cannot be more than the package price.");
      return;
    }
    if (referenceMethods.has(method) && !reference.trim()) {
      setFormError("Enter a reference number for this payment method.");
      return;
    }
    setFormError(null);
    assign.mutate({ amount, method, payDate, reference });
  }

  return (
    <FormDialog onCancel={onCancel} title="PT package payment">
      <div className="grid gap-3">
        <p className="text-sm text-muted">{packageName} · {price} {currency}. Record what was collected, or assign the package unpaid.</p>
        <input aria-label="PT payment amount" className="field" min="0" onChange={(event) => setAmount(event.target.value)} placeholder={`Amount paid, up to ${price} ${currency}`} step="0.01" type="number" value={amount} />
        <select aria-label="PT payment method" className="field" onChange={(event) => setMethod(event.target.value as (typeof memberPaymentMethods)[number])} value={method}>
          {memberPaymentMethods.map((item) => <option key={item} value={item}>{item}</option>)}
        </select>
        <input aria-label="PT payment date" className="field" onChange={(event) => setPayDate(event.target.value)} type="date" value={payDate} />
        <input aria-label="PT payment reference" className="field" onChange={(event) => setReference(event.target.value)} placeholder={referenceMethods.has(method) ? "Reference number" : "Reference number, optional"} value={reference} />
        {formError ? <p role="alert">{formError}</p> : null}
        {assign.error instanceof ApiError ? <p role="alert">{assign.error.message}</p> : null}
        <div className="mt-2 flex flex-wrap justify-end gap-3">
          <button className="min-h-11 rounded-full border border-line px-4" disabled={assign.isPending} onClick={() => assign.mutate(null)} type="button">Assign unpaid</button>
          <button className="min-h-11 rounded-full bg-accent px-4 font-semibold text-background disabled:opacity-60" disabled={assign.isPending} onClick={savePayment} type="button">{assign.isPending ? "Saving..." : "Save payment"}</button>
        </div>
      </div>
    </FormDialog>
  );
}
