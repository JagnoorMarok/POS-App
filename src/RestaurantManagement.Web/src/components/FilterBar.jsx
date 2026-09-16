import React from 'react';
import { Search, Sparkles, Leaf, Drumstick } from 'lucide-react';

export default function FilterBar({
  searchQuery,
  onSearchChange,
  dietFilter,
  onDietFilterChange,
  categories,
  selectedCategory,
  onCategorySelect,
  totalProductCount
}) {
  return (
    <div className="controls-card">
      <div className="controls-row-top">
        <div className="search-box">
          <Search size={18} className="search-icon" />
          <input
            type="text"
            className="search-input"
            placeholder="Search dishes, drinks, ingredients..."
            value={searchQuery}
            onChange={(e) => onSearchChange(e.target.value)}
            id="search-input"
          />
        </div>

        <div className="filter-toggles">
          <button
            type="button"
            className={`filter-btn ${dietFilter === 'all' ? 'active' : ''}`}
            onClick={() => onDietFilterChange('all')}
          >
            <Sparkles size={14} />
            <span>All Items</span>
          </button>

          <button
            type="button"
            className={`filter-btn ${dietFilter === 'veg' ? 'active' : ''}`}
            onClick={() => onDietFilterChange('veg')}
          >
            <Leaf size={14} color="#34d399" />
            <span>Vegetarian</span>
          </button>

          <button
            type="button"
            className={`filter-btn ${dietFilter === 'nonveg' ? 'active' : ''}`}
            onClick={() => onDietFilterChange('nonveg')}
          >
            <Drumstick size={14} color="#f87171" />
            <span>Non-Veg</span>
          </button>
        </div>
      </div>

      {/* Category Pills Bar */}
      <div className="categories-bar">
        <button
          type="button"
          className={`category-chip ${selectedCategory === 'all' ? 'active' : ''}`}
          onClick={() => onCategorySelect('all')}
        >
          <span>🍽️ All</span>
          <span className="category-badge-count">{totalProductCount}</span>
        </button>

        {categories.map((cat) => (
          <button
            key={cat.id}
            type="button"
            className={`category-chip ${selectedCategory === cat.id ? 'active' : ''}`}
            onClick={() => onCategorySelect(cat.id)}
          >
            <span>{cat.name}</span>
            <span className="category-badge-count">{cat.productCount || 0}</span>
          </button>
        ))}
      </div>
    </div>
  );
}
