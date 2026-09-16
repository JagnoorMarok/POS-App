import React from 'react';
import { CheckCircle2, AlertCircle, Info } from 'lucide-react';

export default function Toast({ toasts }) {
  if (!toasts || toasts.length === 0) return null;

  return (
    <div className="toast-container">
      {toasts.map((toast) => (
        <div key={toast.id} className={`toast ${toast.type || 'info'}`}>
          {toast.type === 'success' && <CheckCircle2 size={18} color="#34d399" />}
          {toast.type === 'error' && <AlertCircle size={18} color="#f87171" />}
          {toast.type === 'info' && <Info size={18} color="#60a5fa" />}
          <span>{toast.message}</span>
        </div>
      ))}
    </div>
  );
}
