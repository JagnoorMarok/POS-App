import React from 'react';
import { Plus, Minus } from 'lucide-react';

export default function ProductListItem({
  product,
  currencySymbol,
  cartQuantity,
  onAddToCart,
  onUpdateQuantity
}) {
  const isOutOfStock = !product.isAvailable || product.stockStatus === 'OutOfStock';
  const isVeg = product.name?.toLowerCase().includes('veg') && !product.name?.toLowerCase().includes('non-veg');

  return (
    <div className="menu-list-item">
      <div className="item-info">
        <div className="item-title-row">
          <span className={`item-diet-dot ${isVeg ? 'veg' : 'nonveg'}`} title={isVeg ? 'Vegetarian' : 'Non-Vegetarian'} />
          <h3 className="item-name">{product.name}</h3>
        </div>

        {product.description && (
          <p className="item-desc">{product.description}</p>
        )}

        <div className="item-price">
          {currencySymbol || '₹'}{Number(product.price).toFixed(2)}
        </div>
      </div>

      <div className="item-action">
        {isOutOfStock ? (
          <span style={{ fontSize: '0.8rem', color: 'var(--text-muted)' }}>Sold Out</span>
        ) : cartQuantity > 0 ? (
          <div className="stepper">
            <button
              type="button"
              className="stepper-btn"
              onClick={() => onUpdateQuantity(product.id, cartQuantity - 1)}
            >
              <Minus size={14} />
            </button>
            <span style={{ minWidth: '16px', textAlign: 'center', fontSize: '0.88rem' }}>
              {cartQuantity}
            </span>
            <button
              type="button"
              className="stepper-btn"
              onClick={() => onUpdateQuantity(product.id, cartQuantity + 1)}
            >
              <Plus size={14} />
            </button>
          </div>
        ) : (
          <button
            type="button"
            className="btn-add-item"
            onClick={() => onAddToCart(product)}
          >
            <Plus size={14} />
            <span>Add</span>
          </button>
        )}
      </div>
    </div>
  );
}
