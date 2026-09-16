import React, { useState } from 'react';
import { ShoppingBag, X, Plus, Minus, Trash2, Send, MessageSquare } from 'lucide-react';

export default function CartDrawer({
  isOpen,
  onClose,
  cart,
  currencySymbol,
  taxRatePercent,
  table,
  onUpdateQuantity,
  onUpdateNotes,
  onRemoveItem,
  onPlaceOrder,
  isPlacingOrder
}) {
  const [orderNotes, setOrderNotes] = useState('');

  if (!isOpen) return null;

  const subtotal = cart.reduce((sum, item) => sum + (item.price * item.quantity), 0);
  const taxAmount = Math.round(subtotal * (taxRatePercent / 100) * 100) / 100;
  const grandTotal = subtotal + taxAmount;

  const handleSendToKitchen = () => {
    if (cart.length === 0) return;
    onPlaceOrder({
      tableIdentifier: table?.tableNumber || table?.id,
      notes: orderNotes.trim() || null
    });
  };

  return (
    <div className="modal-backdrop" onClick={onClose}>
      <div
        className="modal-card"
        style={{ maxWidth: '480px', maxHeight: '90vh', display: 'flex', flexDirection: 'column' }}
        onClick={(e) => e.stopPropagation()}
      >
        {/* Drawer Header */}
        <div className="modal-header">
          <div style={{ display: 'flex', alignItems: 'center', gap: '0.6rem' }}>
            <ShoppingBag size={22} color="#f59e0b" />
            <h3 className="modal-title">Your Table Order</h3>
            {table && (
              <span className="badge-react" style={{ fontSize: '0.75rem', padding: '0.2rem 0.6rem' }}>
                {table.tableNumber}
              </span>
            )}
          </div>
          <button type="button" className="modal-close" onClick={onClose}>
            <X size={20} />
          </button>
        </div>

        {/* Cart Body */}
        <div className="modal-body" style={{ overflowY: 'auto', flex: 1 }}>
          {cart.length === 0 ? (
            <div className="empty-state" style={{ padding: '2rem 1rem' }}>
              <div className="empty-icon">🍽️</div>
              <h3 className="empty-title">Your cart is empty</h3>
              <p className="empty-text">Add your favorite dishes from the menu to start ordering.</p>
            </div>
          ) : (
            <div style={{ display: 'flex', flexDirection: 'column', gap: '1rem' }}>
              {cart.map((item) => (
                <div
                  key={item.id}
                  style={{
                    background: 'var(--bg-primary)',
                    border: '1px solid var(--border-color)',
                    borderRadius: 'var(--radius-md)',
                    padding: '0.85rem 1rem',
                    display: 'flex',
                    flexDirection: 'column',
                    gap: '0.6rem'
                  }}
                >
                  <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start' }}>
                    <div>
                      <h4 style={{ fontSize: '1rem', fontWeight: 700, color: '#ffffff' }}>{item.name}</h4>
                      <span style={{ fontSize: '0.85rem', color: 'var(--text-muted)' }}>
                        {currencySymbol}{Number(item.price).toFixed(2)} each
                      </span>
                    </div>

                    <span style={{ fontSize: '1.05rem', fontWeight: 800, color: 'var(--accent-primary)' }}>
                      {currencySymbol}{Number(item.price * item.quantity).toFixed(2)}
                    </span>
                  </div>

                  {/* Quantity Stepper & Remove */}
                  <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginTop: '0.25rem' }}>
                    <div style={{ display: 'flex', alignItems: 'center', gap: '0.6rem', background: 'var(--bg-surface-elevated)', borderRadius: 'var(--radius-sm)', padding: '0.2rem 0.5rem' }}>
                      <button
                        type="button"
                        onClick={() => onUpdateQuantity(item.id, item.quantity - 1)}
                        style={{ background: 'transparent', border: 'none', color: '#ffffff', cursor: 'pointer', display: 'flex', padding: '0.2rem' }}
                      >
                        <Minus size={15} />
                      </button>
                      <span style={{ fontWeight: 700, fontSize: '0.95rem', minWidth: '20px', textAlign: 'center' }}>
                        {item.quantity}
                      </span>
                      <button
                        type="button"
                        onClick={() => onUpdateQuantity(item.id, item.quantity + 1)}
                        style={{ background: 'transparent', border: 'none', color: '#ffffff', cursor: 'pointer', display: 'flex', padding: '0.2rem' }}
                      >
                        <Plus size={15} />
                      </button>
                    </div>

                    <button
                      type="button"
                      onClick={() => onRemoveItem(item.id)}
                      style={{ background: 'transparent', border: 'none', color: '#f87171', cursor: 'pointer', display: 'flex', alignItems: 'center', gap: '0.3rem', fontSize: '0.8rem' }}
                    >
                      <Trash2 size={15} />
                      <span>Remove</span>
                    </button>
                  </div>

                  {/* Special Cooking Note for Item */}
                  <div style={{ marginTop: '0.25rem' }}>
                    <input
                      type="text"
                      className="form-input"
                      placeholder="Special note (e.g. less spicy, no onion)..."
                      value={item.notes || ''}
                      onChange={(e) => onUpdateNotes(item.id, e.target.value)}
                      style={{ fontSize: '0.8rem', padding: '0.4rem 0.65rem' }}
                    />
                  </div>
                </div>
              ))}

              {/* Order Level Instructions */}
              <div className="form-group" style={{ marginTop: '0.5rem' }}>
                <label className="form-label" style={{ display: 'flex', alignItems: 'center', gap: '0.4rem' }}>
                  <MessageSquare size={14} color="#f59e0b" />
                  <span>General Order Notes (Optional)</span>
                </label>
                <textarea
                  className="form-textarea"
                  value={orderNotes}
                  onChange={(e) => setOrderNotes(e.target.value)}
                  placeholder="e.g. Serve starters together, extra plates please..."
                  style={{ minHeight: '60px', fontSize: '0.85rem' }}
                />
              </div>

              {/* Bill Breakdown */}
              <div style={{
                background: 'rgba(0, 0, 0, 0.2)',
                borderRadius: 'var(--radius-md)',
                padding: '1rem',
                border: '1px solid var(--border-color)',
                display: 'flex',
                flexDirection: 'column',
                gap: '0.4rem',
                fontSize: '0.9rem'
              }}>
                <div style={{ display: 'flex', justifyContent: 'space-between', color: 'var(--text-muted)' }}>
                  <span>Subtotal</span>
                  <span>{currencySymbol}{subtotal.toFixed(2)}</span>
                </div>
                <div style={{ display: 'flex', justifyContent: 'space-between', color: 'var(--text-muted)' }}>
                  <span>Tax ({taxRatePercent}%)</span>
                  <span>{currencySymbol}{taxAmount.toFixed(2)}</span>
                </div>
                <div style={{
                  display: 'flex',
                  justifyContent: 'space-between',
                  fontWeight: 800,
                  fontSize: '1.15rem',
                  color: '#ffffff',
                  borderTop: '1px solid var(--border-color)',
                  paddingTop: '0.5rem',
                  marginTop: '0.3rem'
                }}>
                  <span>Total Amount</span>
                  <span style={{ color: 'var(--accent-primary)' }}>{currencySymbol}{grandTotal.toFixed(2)}</span>
                </div>
              </div>
            </div>
          )}
        </div>

        {/* Drawer Footer */}
        {cart.length > 0 && (
          <div className="modal-footer" style={{ justifyContent: 'space-between', alignItems: 'center' }}>
            <div>
              <span style={{ fontSize: '0.78rem', color: 'var(--text-muted)' }}>Total: </span>
              <strong style={{ fontSize: '1.2rem', color: '#ffffff' }}>
                {currencySymbol}{grandTotal.toFixed(2)}
              </strong>
            </div>

            <button
              type="button"
              className="btn-primary"
              onClick={handleSendToKitchen}
              disabled={isPlacingOrder}
              style={{ padding: '0.75rem 1.5rem', fontSize: '0.95rem' }}
            >
              <Send size={16} />
              <span>{isPlacingOrder ? 'Sending...' : 'Send to Kitchen'}</span>
            </button>
          </div>
        )}
      </div>
    </div>
  );
}
