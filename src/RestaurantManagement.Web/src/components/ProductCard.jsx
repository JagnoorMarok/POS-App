import React from 'react';
import { Plus, Minus, Check } from 'lucide-react';

export default function ProductCard({
  product,
  currencySymbol,
  cartQuantity,
  onAddToCart,
  onUpdateQuantity
}) {
  const isOutOfStock = !product.isAvailable || product.stockStatus === 'OutOfStock';
  const formattedPrice = `${currencySymbol || '₹'}${Number(product.price).toFixed(2)}`;

  return (
    <div className={`product-card ${isOutOfStock ? 'out-of-stock' : ''}`}>
      <div>
        <div className="card-top">
          <div className="card-title-group">
            <h3 className="card-title">{product.name}</h3>
            <div className="card-category">{product.categoryName}</div>
          </div>

          <span className={`status-tag ${isOutOfStock ? 'out-stock' : 'in-stock'}`}>
            {isOutOfStock ? '✕ Sold Out' : '● In Stock'}
          </span>
        </div>

        <p className="card-description">
          {product.description || 'Freshly prepared with authentic ingredients and flavors.'}
        </p>
      </div>

      <div className="card-bottom">
        <div className="card-price">{formattedPrice}</div>

        <div className="card-actions">
          {isOutOfStock ? (
            <span style={{ fontSize: '0.8rem', color: 'var(--text-subtle)', fontStyle: 'italic' }}>
              Unavailable
            </span>
          ) : cartQuantity > 0 ? (
            <div style={{
              display: 'flex',
              alignItems: 'center',
              gap: '0.5rem',
              background: 'var(--accent-primary)',
              color: '#0b1120',
              borderRadius: 'var(--radius-sm)',
              padding: '0.25rem 0.6rem',
              fontWeight: 800
            }}>
              <button
                type="button"
                onClick={() => onUpdateQuantity(product.id, cartQuantity - 1)}
                style={{ background: 'transparent', border: 'none', color: '#0b1120', cursor: 'pointer', display: 'flex' }}
              >
                <Minus size={15} />
              </button>
              <span style={{ minWidth: '18px', textAlign: 'center', fontSize: '0.9rem' }}>
                {cartQuantity}
              </span>
              <button
                type="button"
                onClick={() => onUpdateQuantity(product.id, cartQuantity + 1)}
                style={{ background: 'transparent', border: 'none', color: '#0b1120', cursor: 'pointer', display: 'flex' }}
              >
                <Plus size={15} />
              </button>
            </div>
          ) : (
            <button
              type="button"
              className="btn-primary"
              onClick={() => onAddToCart(product)}
              style={{ padding: '0.45rem 0.95rem', fontSize: '0.85rem' }}
            >
              <Plus size={15} />
              <span>Add</span>
            </button>
          )}
        </div>
      </div>
    </div>
  );
}
