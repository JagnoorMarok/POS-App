import React from 'react';
import { UtensilsCrossed, ShoppingBag, Armchair, Clock } from 'lucide-react';

export default function Navbar({
  restaurant,
  table,
  onOpenTableModal,
  cartCount,
  onOpenCart,
  activeOrder,
  onOpenActiveOrder
}) {
  return (
    <header className="app-header">
      <div className="header-inner">
        <div className="brand-group">
          <div className="brand-icon">
            <UtensilsCrossed size={24} />
          </div>
          <div className="brand-text">
            <h1>{restaurant.restaurantName || 'Restaurant Menu'}</h1>
            <p>Digital Table Ordering</p>
          </div>
        </div>

        <div className="header-actions">
          {/* Table Badge / Selector */}
          <button
            type="button"
            className="db-pill"
            onClick={onOpenTableModal}
            style={{ cursor: 'pointer', background: 'rgba(245, 158, 11, 0.15)', borderColor: 'rgba(245, 158, 11, 0.35)', color: '#fbbf24' }}
            title="Click to switch table"
          >
            <Armchair size={15} />
            <span>{table ? `Table ${table.tableNumber}` : 'Select Table'}</span>
          </button>

          {/* Active Live Order Pill */}
          {activeOrder && (
            <button
              type="button"
              className="badge-react"
              onClick={onOpenActiveOrder}
              style={{ cursor: 'pointer', background: 'rgba(16, 185, 129, 0.15)', borderColor: 'rgba(16, 185, 129, 0.35)', color: '#34d399' }}
            >
              <Clock size={15} />
              <span>Order #{activeOrder.orderNumber} ({activeOrder.statusName})</span>
            </button>
          )}

          {/* View Cart Button */}
          <button
            className="btn-primary"
            onClick={onOpenCart}
            type="button"
            id="btn-open-cart"
            style={{ position: 'relative' }}
          >
            <ShoppingBag size={18} />
            <span>Cart</span>
            {cartCount > 0 && (
              <span style={{
                background: '#0b1120',
                color: 'var(--accent-primary)',
                borderRadius: 'var(--radius-full)',
                padding: '0.15rem 0.5rem',
                fontSize: '0.8rem',
                fontWeight: 900,
                marginLeft: '0.2rem'
              }}>
                {cartCount}
              </span>
            )}
          </button>
        </div>
      </div>
    </header>
  );
}
