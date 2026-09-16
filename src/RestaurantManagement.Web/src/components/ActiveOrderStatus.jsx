import React from 'react';
import { Clock, Flame, ChefHat, Utensils, Plus } from 'lucide-react';

export default function ActiveOrderStatus({
  activeOrder,
  currencySymbol,
  onOrderMore
}) {
  if (!activeOrder) return null;

  const getStepIndex = (status) => {
    switch (status) {
      case 2: return 1; // Confirmed
      case 3: return 2; // Preparing
      case 4: return 3; // Ready
      case 5: return 4; // Served
      default: return 1;
    }
  };

  const step = getStepIndex(activeOrder.status);

  return (
    <div style={{
      background: 'var(--bg-surface)',
      border: '1px solid var(--border-subtle)',
      borderRadius: 'var(--radius-lg)',
      padding: '1.25rem 1.5rem',
      marginBottom: '1.75rem',
      boxShadow: 'var(--shadow-sm)'
    }}>
      <div style={{
        display: 'flex',
        justifyContent: 'space-between',
        alignItems: 'center',
        flexWrap: 'wrap',
        gap: '1rem',
        borderBottom: '1px solid var(--border-subtle)',
        paddingBottom: '0.85rem'
      }}>
        <div>
          <span style={{ fontSize: '0.78rem', textTransform: 'uppercase', letterSpacing: '0.04em', color: 'var(--text-secondary)', fontWeight: 600 }}>
            Order #{activeOrder.orderNumber}
          </span>
          <h2 style={{ fontFamily: 'var(--font-heading)', fontSize: '1.25rem', fontWeight: 800, color: 'var(--text-primary)', marginTop: '0.15rem' }}>
            Table {activeOrder.tableNumber}
          </h2>
        </div>

        <div style={{ display: 'flex', alignItems: 'center', gap: '1rem' }}>
          <div style={{ textAlign: 'right' }}>
            <span style={{ fontSize: '0.75rem', color: 'var(--text-muted)' }}>Bill Amount</span>
            <div style={{ fontWeight: 800, fontSize: '1.15rem', color: 'var(--text-primary)' }}>
              {currencySymbol}{Number(activeOrder.totalAmount).toFixed(2)}
            </div>
          </div>

          <button
            type="button"
            className="btn-add-item"
            onClick={onOrderMore}
            style={{ padding: '0.45rem 0.95rem' }}
          >
            <Plus size={14} />
            <span>Order More</span>
          </button>
        </div>
      </div>

      {/* Status Progress Stepper */}
      <div style={{ display: 'flex', justifyContent: 'space-between', margin: '1.25rem 0 0.5rem', gap: '0.5rem' }}>
        <div style={{ display: 'flex', flexDirection: 'column', alignItems: 'center', flex: 1 }}>
          <div style={{
            width: '32px',
            height: '32px',
            borderRadius: '50%',
            background: step >= 1 ? 'var(--accent-color)' : 'var(--bg-surface-subtle)',
            color: step >= 1 ? '#ffffff' : 'var(--text-muted)',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center'
          }}>
            <Clock size={16} />
          </div>
          <span style={{ fontSize: '0.75rem', fontWeight: 500, color: step >= 1 ? 'var(--text-primary)' : 'var(--text-muted)', marginTop: '0.35rem', textAlign: 'center' }}>
            Received
          </span>
        </div>

        <div style={{ display: 'flex', flexDirection: 'column', alignItems: 'center', flex: 1 }}>
          <div style={{
            width: '32px',
            height: '32px',
            borderRadius: '50%',
            background: step >= 2 ? 'var(--accent-color)' : 'var(--bg-surface-subtle)',
            color: step >= 2 ? '#ffffff' : 'var(--text-muted)',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center'
          }}>
            <Flame size={16} />
          </div>
          <span style={{ fontSize: '0.75rem', fontWeight: 500, color: step >= 2 ? 'var(--text-primary)' : 'var(--text-muted)', marginTop: '0.35rem', textAlign: 'center' }}>
            In Kitchen
          </span>
        </div>

        <div style={{ display: 'flex', flexDirection: 'column', alignItems: 'center', flex: 1 }}>
          <div style={{
            width: '32px',
            height: '32px',
            borderRadius: '50%',
            background: step >= 3 ? 'var(--accent-color)' : 'var(--bg-surface-subtle)',
            color: step >= 3 ? '#ffffff' : 'var(--text-muted)',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center'
          }}>
            <ChefHat size={16} />
          </div>
          <span style={{ fontSize: '0.75rem', fontWeight: 500, color: step >= 3 ? 'var(--text-primary)' : 'var(--text-muted)', marginTop: '0.35rem', textAlign: 'center' }}>
            Ready
          </span>
        </div>

        <div style={{ display: 'flex', flexDirection: 'column', alignItems: 'center', flex: 1 }}>
          <div style={{
            width: '32px',
            height: '32px',
            borderRadius: '50%',
            background: step >= 4 ? 'var(--color-veg)' : 'var(--bg-surface-subtle)',
            color: step >= 4 ? '#ffffff' : 'var(--text-muted)',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center'
          }}>
            <Utensils size={16} />
          </div>
          <span style={{ fontSize: '0.75rem', fontWeight: 500, color: step >= 4 ? 'var(--color-veg)' : 'var(--text-muted)', marginTop: '0.35rem', textAlign: 'center' }}>
            Served
          </span>
        </div>
      </div>
    </div>
  );
}
