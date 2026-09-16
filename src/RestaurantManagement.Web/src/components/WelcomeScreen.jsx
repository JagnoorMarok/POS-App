import React from 'react';
import { Utensils, Clock, ShieldCheck, ArrowRight, Sparkles } from 'lucide-react';

export default function WelcomeScreen({ restaurant, onExploreMenu }) {
  return (
    <div className="welcome-screen">
      <div className="welcome-badge">
        <Sparkles size={14} color="var(--text-secondary)" />
        <span>Welcome to our Digital Menu</span>
      </div>

      <h1 className="welcome-title">{restaurant.restaurantName || 'The Grand Restaurant'}</h1>
      <p className="welcome-tagline">
        Freshly prepared authentic gourmet dishes crafted with premium ingredients and passion.
      </p>

      <div className="welcome-card-grid">
        <div className="welcome-info-card">
          <Utensils size={20} color="var(--text-primary)" />
          <h4>Authentic Cuisine</h4>
          <p>Handcrafted recipes</p>
        </div>

        <div className="welcome-info-card">
          <Clock size={20} color="var(--text-primary)" />
          <h4>Fresh & Fast</h4>
          <p>Direct kitchen ordering</p>
        </div>

        <div className="welcome-info-card">
          <ShieldCheck size={20} color="var(--text-primary)" />
          <h4>Hygiene Promise</h4>
          <p>Top quality standards</p>
        </div>
      </div>

      <button
        type="button"
        className="btn-start-order"
        onClick={onExploreMenu}
        id="btn-explore-menu"
      >
        <span>Explore Menu & Order</span>
        <ArrowRight size={18} />
      </button>
    </div>
  );
}
