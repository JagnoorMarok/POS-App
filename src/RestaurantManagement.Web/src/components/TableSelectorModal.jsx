import React from 'react';
import { Armchair, X } from 'lucide-react';

export default function TableSelectorModal({
  isOpen,
  onClose,
  tables,
  selectedTable,
  onSelectTable
}) {
  if (!isOpen) return null;

  return (
    <div className="modal-backdrop" onClick={onClose}>
      <div className="modal-card" onClick={(e) => e.stopPropagation()}>
        <div className="modal-header">
          <h3 className="modal-title">Select Your Table</h3>
          {selectedTable && (
            <button type="button" className="modal-close" onClick={onClose}>
              <X size={20} />
            </button>
          )}
        </div>

        <div className="modal-body">
          <p style={{ color: 'var(--text-muted)', fontSize: '0.9rem', marginBottom: '1rem' }}>
            Please select the table you are currently seated at to place your order:
          </p>

          <div style={{
            display: 'grid',
            gridTemplateColumns: 'repeat(auto-fill, minmax(120px, 1fr))',
            gap: '0.85rem'
          }}>
            {tables.map((table) => {
              const isSelected = selectedTable?.id === table.id;
              return (
                <button
                  key={table.id}
                  type="button"
                  onClick={() => {
                    onSelectTable(table);
                    onClose();
                  }}
                  style={{
                    background: isSelected ? 'var(--accent-primary)' : 'var(--bg-primary)',
                    color: isSelected ? '#0b1120' : 'var(--text-main)',
                    border: isSelected ? '2px solid var(--accent-primary)' : '1px solid var(--border-color)',
                    borderRadius: 'var(--radius-md)',
                    padding: '1.25rem 0.75rem',
                    display: 'flex',
                    flexDirection: 'column',
                    alignItems: 'center',
                    gap: '0.5rem',
                    cursor: 'pointer',
                    transition: 'all 0.2s ease',
                    boxShadow: isSelected ? '0 4px 14px var(--accent-glow)' : 'none'
                  }}
                >
                  <Armchair size={28} />
                  <span style={{ fontWeight: 700, fontSize: '1.05rem' }}>{table.tableNumber}</span>
                  <span style={{ fontSize: '0.75rem', opacity: 0.8 }}>Seats {table.capacity}</span>
                </button>
              );
            })}
          </div>
        </div>
      </div>
    </div>
  );
}
