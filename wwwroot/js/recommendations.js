// Vehicle recommendation system
class VehicleRecommendationEngine {
    constructor() {
        this.userPreferences = this.loadUserPreferences();
        this.recommendations = [];
    }

    loadUserPreferences() {
        const saved = localStorage.getItem('userPreferences');
        return saved ? JSON.parse(saved) : {
            priceRange: { min: 0, max: 500 },
            fuelType: 'all',
            preferredStates: [],
            preferredCategories: []
        };
    }

    saveUserPreferences(preferences) {
        this.userPreferences = { ...this.userPreferences, ...preferences };
        localStorage.setItem('userPreferences', JSON.stringify(this.userPreferences));
    }

    async getRecommendations(userId = null) {
        // Fetch recommendations from API
        const url = userId 
            ? `/Cars/GetRecommendations?userId=${userId}`
            : '/Cars/GetRecommendations';
        
        try {
            const response = await fetch(url);
            const data = await response.json();
            this.recommendations = data;
            return this.recommendations;
        } catch (error) {
            console.error('Error fetching recommendations:', error);
            return [];
        }
    }

    calculateRecommendationScore(car, preferences) {
        let score = 100;
        
        // Price preference (0-40 points)
        if (car.dailyRate < preferences.priceRange.min || car.dailyRate > preferences.priceRange.max) {
            score -= 40;
        } else {
            const priceDiff = Math.abs(car.dailyRate - (preferences.priceRange.min + preferences.priceRange.max) / 2);
            const priceRange = preferences.priceRange.max - preferences.priceRange.min;
            score -= (priceDiff / priceRange) * 40;
        }
        
        // Fuel type preference (0-30 points)
        if (preferences.fuelType !== 'all') {
            const isElectric = car.isElectric;
            if ((preferences.fuelType === 'electric' && !isElectric) || 
                (preferences.fuelType === 'gas' && isElectric)) {
                score -= 30;
            }
        }
        
        // Location preference (0-20 points)
        if (preferences.preferredStates.length > 0) {
            if (!preferences.preferredStates.includes(car.state)) {
                score -= 20;
            }
        }
        
        // Category preference (0-10 points)
        if (preferences.preferredCategories.length > 0) {
            if (!preferences.preferredCategories.includes(car.categoryId)) {
                score -= 10;
            }
        }
        
        return Math.max(0, score);
    }

    displayRecommendations(containerId) {
        const container = document.getElementById(containerId);
        if (!container || this.recommendations.length === 0) return;
        
        container.innerHTML = this.recommendations.map(car => `
            <div class="recommendation-card fade-in" data-score="${car.recommendationScore}">
                <div class="recommendation-badge">
                    <span class="material-symbols-outlined">star</span>
                    ${car.recommendationScore}% Match
                </div>
                <img src="${car.imageUrl}" alt="${car.displayName}" />
                <div class="recommendation-content">
                    <h5>${car.displayName}</h5>
                    <p class="text-muted">${car.categoryName}</p>
                    <div class="recommendation-features">
                        <span><i class="fas fa-dollar-sign"></i> $${car.dailyRate}/day</span>
                        <span><i class="fas fa-map-marker-alt"></i> ${car.city}, ${car.state}</span>
                    </div>
                    <a href="/Cars/Details/${car.id}" class="btn btn-primary btn-sm mt-2">
                        View Details
                    </a>
                </div>
            </div>
        `).join('');
    }
}

// Initialize recommendation engine
const recommendationEngine = new VehicleRecommendationEngine();

document.addEventListener('DOMContentLoaded', function() {
    // Load recommendations on page load
    recommendationEngine.getRecommendations().then(() => {
        recommendationEngine.displayRecommendations('recommendations-container');
    });
});

