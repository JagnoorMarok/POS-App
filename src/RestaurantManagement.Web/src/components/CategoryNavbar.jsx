import React from 'react';

export default function CategoryNavbar({
  categories,
  selectedCategory,
  onSelectCategory
}) {
  return (
    <nav className="category-nav-bar">
      <div className="category-nav-inner">
        <button
          type="button"
          className={`cat-nav-btn ${selectedCategory === 'all' ? 'active' : ''}`}
          onClick={() => onSelectCategory('all')}
        >
          All Items
        </button>

        {categories.map((cat) => (
          <button
            key={cat.id}
            type="button"
            className={`cat-nav-btn ${selectedCategory === cat.id ? 'active' : ''}`}
            onClick={() => onSelectCategory(cat.id)}
          >
            {cat.name}
          </button>
        ))}
      </div>
    </nav>
  );
}
