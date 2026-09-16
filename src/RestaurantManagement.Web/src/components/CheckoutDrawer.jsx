import React, { useState } from 'react';
import { X, Plus, Minus, Trash2, Send, Armchair, MessageSquare } from 'lucide-react';

export default function CheckoutDrawer({
  isOpen,
  onClose,
  cart,
  tables,
  selectedTable,
  onSelectTable,
  currencySymbol,
  taxRatePercent,
  onUpdateQuantity,
  onUpdateNotes,
  onRemoveItem,
  onConfirmOrder,
  isPlacingOrder
}) {
  const [orderNotes, setOrderNotes] = useState('');
  const [customTableInput, setCustomTableInput] = useState('');

  if (!isOpen) return null;

  const subtotal = cart.reduce((sum, item) => sum + (item.price * item.quantity), 0);
  const taxAmount = Math.round(subtotal * (taxRatePercent / 100) * 100) / 100;
  const grandTotal = subtotal + taxAmount;

  const handlePlaceOrder = () => {
    const tableId = selectedTable?.tableNumber || selectedTable?.id || customTableInput.trim();
    if (!tableId) {
      alert('Please select or enter your Table Number before placing the order.');
      return;
    }

    onConfirmOrder({
      tableIdentifier: tableId,
      notes: orderNotes.trim() || null
    });
  };

  return (
    <div className="checkout-modal-backdrop" onClick={onClose}>
      <div className="checkout-modal-card" onClick={(e) => e.stopPropagation()}>
        
        {/* Header */}
        <div className="checkout-header">
          <div>
            <h3 style={{ fontFamily: 'var(--font-heading)', fontSize: '1.2rem', fontWeight: 700, color: 'var(--text-primary)' }}>
              Review Order
            </h3>
            <span style={{ fontSize: '0.8rem', color: 'var(--text-secondary)' }}>
              {cart.length} {cart.length === 1 ? 'item' : 'items'} in your order
            </span>
          </div>
          <button type="button" className="stepper-btn" onClick={onClose} style={{ color: 'var(--text-secondary)', background: 'transparent' }}>
            <X size={20} />
          </button>
        </div>

        {/* Body */}
        <div className="checkout-body">
          
          {/* Table Selection Prompt */}
          <div className="table-prompt-box">
            <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem', marginBottom: '0.35rem' }}>
              <Armchair size={17} color="var(--text-primary)" />
              <h4>Which table are you seated at? *</h4>
            </div>
            <p>Select your table number so the kitchen delivers food to your table:</p>

            <div className="table-pill-grid">
              {tables.map((tbl) => {
                const isSelected = selectedTable?.id === tbl.id || selectedTable?.tableNumber === tbl.tableNumber;
                return (
                  <button
                    key={tbl.id}
                    type="button"
                    className={`table-select-pill ${isSelected ? 'selected' : ''}`}
                    onClick={() => {
                      onSelectTable(tbl);
                      setCustomTableInput('');
                    }}
                  >
                    {tbl.tableNumber}
                  </button>
                );
              })}
            </div>
          </div>

          {/* Items Review */}
          <div style={{ display: 'flex', flexDirection: 'column', gap: '0.75rem' }}>
            <span style={{ fontSize: '0.82rem', fontWeight: 600, textTransform: 'uppercase', letterSpacing: '0.04em', color: 'var(--text-muted)' }}>
              Order Items
            </span>

            {cart.map((item) => (
              <div
                key={item.id}
                style={{
                  background: 'var(--bg-surface)',
                  border: '1px solid var(--border-subtle)',
                  borderRadius: 'var(--radius-md)',
                  padding: '0.85rem 1rem',
                  display: 'flex',
                  flexDirection: 'column',
                  gap: '0.5rem',
                  boxShadow: 'var(--shadow-sm)'
                }}
              >
                <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                  <span style={{ fontWeight: 600, fontSize: '0.95rem', color: 'var(--text-primary)' }}>{item.name}</span>
                  <span style={{ fontWeight: 700, color: 'var(--text-primary)' }}>
                    {currencySymbol}{Number(item.price * item.quantity).toFixed(2)}
                  </span>
                </div>

                <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                  <div className="stepper">
                    <button
                      type="button"
                      className="stepper-btn"
                      onClick={() => onUpdateQuantity(item.id, item.quantity - 1)}
                    >
                      <Minus size={13} />
                    </button>
                    <span style={{ minWidth: '16px', textAlign: 'center', fontSize: '0.85rem' }}>
                      {item.quantity}
                    </span>
                    <button
                      type="button"
                      className="stepper-btn"
                      onClick={() => onUpdateQuantity(item.id, item.quantity + 1)}
                    >
                      <Plus size={13} />
                    </button>
                  </div>

                  <button
                    type="button"
                    onClick={() => onRemoveItem(item.id)}
                    style={{ background: 'transparent', border: 'none', color: '#dc2626', cursor: 'pointer', display: 'flex', alignItems: 'center', gap: '0.25rem', fontSize: '0.78rem', fontWeight: 500 }}
                  >
                    <Trash2 size={13} />
                    <span>Remove</span>
                  </button>
                </div>

                <input
                  type="text"
                  placeholder="Special cooking note (e.g. less spicy)..."
                  value={item.notes || ''}
                  onChange={(e) => onUpdateNotes(item.id, e.target.value)}
                  style={{
                    background: 'var(--bg-surface-subtle)',
                    border: '1px solid var(--border-subtle)',
                    borderRadius: 'var(--radius-sm)',
                    padding: '0.4rem 0.65rem',
                    color: 'var(--text-primary)',
                    fontSize: '0.8rem',
                    outline: 'none'
                  }}
                />
              </div>
            ))}
          </div>

          {/* General Order Instructions */}
          <div style={{ display: 'flex', flexDirection: 'column', gap: '0.35rem' }}>
            <label style={{ fontSize: '0.82rem', fontWeight: 600, color: 'var(--text-secondary)', display: 'flex', alignItems: 'center', gap: '0.4rem' }}>
              <MessageSquare size={14} color="var(--text-secondary)" />
              <span>General Notes for Chef (Optional)</span>
            </label>
            <textarea
              placeholder="e.g. Please serve all items together..."
              value={orderNotes}
              onChange={(e) => setOrderNotes(e.target.value)}
              style={{
                background: 'var(--bg-surface)',
                border: '1px solid var(--border-subtle)',
                borderRadius: 'var(--radius-sm)',
                padding: '0.5rem 0.75rem',
                color: 'var(--text-primary)',
                fontSize: '0.85rem',
                minHeight: '55px',
                outline: 'none',
                fontFamily: 'inherit'
              }}
            />
          </div>

          {/* Bill Summary */}
          <div style={{
            background: 'var(--bg-surface-subtle)',
            border: '1px solid var(--border-subtle)',
            borderRadius: 'var(--radius-md)',
            padding: '0.85rem 1rem',
            display: 'flex',
            flexDirection: 'column',
            gap: '0.35rem',
            fontSize: '0.88rem'
          }}>
            <div style={{ display: 'flex', justifyContent: 'space-between', color: 'var(--text-secondary)' }}>
              <span>Subtotal</span>
              <span>{currencySymbol}{subtotal.toFixed(2)}</span>
            </div>
            <div style={{ display: 'flex', justifyContent: 'space-between', color: 'var(--text-secondary)' }}>
              <span>Tax ({taxRatePercent}%)</span>
              <span>{currencySymbol}{taxAmount.toFixed(2)}</span>
            </div>
            <div style={{
              display: 'flex',
              justifyContent: 'space-between',
              fontWeight: 700,
              fontSize: '1.05rem',
              color: 'var(--text-primary)',
              borderTop: '1px solid var(--border-subtle)',
              paddingTop: '0.4rem',
              marginTop: '0.2rem'
            }}>
              <span>Total Payable</span>
              <span>{currencySymbol}{grandTotal.toFixed(2)}</span>
            </div>
          </div>
        </div>

        {/* Footer */}
        <div className="checkout-footer">
          <div>
            <span style={{ fontSize: '0.78rem', color: 'var(--text-muted)' }}>
              {selectedTable ? `Table: ${selectedTable.tableNumber}` : 'Table not chosen'}
            </span>
            <div style={{ fontWeight: 800, fontSize: '1.15rem', color: 'var(--text-primary)' }}>
              {currencySymbol}{grandTotal.toFixed(2)}
            </div>
          </div>

          <button
            type="button"
            className="btn-start-order"
            onClick={handlePlaceOrder}
            disabled={isPlacingOrder || cart.length === 0}
            style={{ padding: '0.75rem 1.75rem', fontSize: '0.95rem' }}
          >
            <Send size={15} />
            <span>{isPlacingOrder ? 'Sending...' : 'Confirm & Place Order'}</span>
          </button>
        </div>
      </div>
    </div>
  );
}
