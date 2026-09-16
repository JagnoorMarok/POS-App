import React, { useState, useEffect } from 'react';
import { X } from 'lucide-react';

export default function ItemModal({
  isOpen,
  onClose,
  product,
  categories,
  onSave,
  onDelete
}) {
  const [name, setName] = useState('');
  const [categoryId, setCategoryId] = useState('');
  const [price, setPrice] = useState('');
  const [description, setDescription] = useState('');
  const [isAvailable, setIsAvailable] = useState(true);
  const [displayOrder, setDisplayOrder] = useState(0);

  useEffect(() => {
    if (product) {
      setName(product.name || '');
      setCategoryId(product.categoryId || (categories[0]?.id || ''));
      setPrice(product.price !== undefined ? product.price : '');
      setDescription(product.description || '');
      setIsAvailable(product.isAvailable !== false);
      setDisplayOrder(product.displayOrder || 0);
    } else {
      setName('');
      setCategoryId(categories[0]?.id || '');
      setPrice('');
      setDescription('');
      setIsAvailable(true);
      setDisplayOrder(0);
    }
  }, [product, categories, isOpen]);

  if (!isOpen) return null;

  const handleSubmit = (e) => {
    e.preventDefault();
    if (!name.trim() || price === '' || isNaN(price) || Number(price) < 0 || !categoryId) {
      alert('Please fill in valid name, price, and category.');
      return;
    }

    onSave({
      id: product?.id,
      name: name.trim(),
      categoryId,
      price: parseFloat(price),
      description: description.trim() || null,
      isAvailable,
      displayOrder: parseInt(displayOrder) || 0,
      isActive: true
    });
  };

  return (
    <div className="modal-backdrop" onClick={onClose}>
      <div className="modal-card" onClick={(e) => e.stopPropagation()}>
        <div className="modal-header">
          <h3 className="modal-title">{product ? 'Edit Menu Item' : 'Add New Menu Item'}</h3>
          <button type="button" className="modal-close" onClick={onClose}>
            <X size={20} />
          </button>
        </div>

        <form onSubmit={handleSubmit}>
          <div className="modal-body">
            <div className="form-group">
              <label className="form-label">Item Name *</label>
              <input
                type="text"
                className="form-input"
                required
                value={name}
                onChange={(e) => setName(e.target.value)}
                placeholder="e.g. Butter Chicken"
              />
            </div>

            <div className="form-row">
              <div className="form-group">
                <label className="form-label">Category *</label>
                <select
                  className="form-select"
                  required
                  value={categoryId}
                  onChange={(e) => setCategoryId(e.target.value)}
                >
                  {categories.map((c) => (
                    <option key={c.id} value={c.id}>
                      {c.name}
                    </option>
                  ))}
                </select>
              </div>

              <div className="form-group">
                <label className="form-label">Price (₹) *</label>
                <input
                  type="number"
                  step="0.01"
                  min="0"
                  className="form-input"
                  required
                  value={price}
                  onChange={(e) => setPrice(e.target.value)}
                  placeholder="0.00"
                />
              </div>
            </div>

            <div className="form-group">
              <label className="form-label">Description</label>
              <textarea
                className="form-textarea"
                value={description}
                onChange={(e) => setDescription(e.target.value)}
                placeholder="Brief description, notes, dietary info..."
              />
            </div>

            <div className="form-row">
              <div className="form-group" style={{ flexDirection: 'row', alignItems: 'center', gap: '0.5rem', marginTop: '0.5rem' }}>
                <input
                  type="checkbox"
                  id="chk-available"
                  checked={isAvailable}
                  onChange={(e) => setIsAvailable(e.target.checked)}
                  style={{ width: '18px', height: '18px', accentColor: 'var(--accent-primary)' }}
                />
                <label htmlFor="chk-available" className="form-label" style={{ margin: 0, cursor: 'pointer' }}>
                  Available / In Stock
                </label>
              </div>

              <div className="form-group">
                <label className="form-label">Display Order</label>
                <input
                  type="number"
                  className="form-input"
                  value={displayOrder}
                  onChange={(e) => setDisplayOrder(e.target.value)}
                />
              </div>
            </div>
          </div>

          <div className="modal-footer">
            {product && (
              <button
                type="button"
                className="btn-danger"
                onClick={() => onDelete(product)}
              >
                Delete
              </button>
            )}
            <button type="button" className="btn-secondary" onClick={onClose}>
              Cancel
            </button>
            <button type="submit" className="btn-primary">
              Save Changes
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}
