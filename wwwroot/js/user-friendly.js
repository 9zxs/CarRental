// User-friendly enhancements
(function() {
    'use strict';

    // Initialize tooltips
    function initTooltips() {
        const tooltipTriggerList = [].slice.call(document.querySelectorAll('[data-bs-toggle="tooltip"]'));
        if (typeof bootstrap !== 'undefined' && bootstrap.Tooltip) {
            tooltipTriggerList.map(function (tooltipTriggerEl) {
                return new bootstrap.Tooltip(tooltipTriggerEl);
            });
        }
    }

    // Add loading spinner to buttons on form submit
    function initFormLoading() {
        document.querySelectorAll('form').forEach(form => {
            form.addEventListener('submit', function(e) {
                const submitButton = form.querySelector('button[type="submit"], input[type="submit"]');
                if (submitButton && !submitButton.disabled) {
                    const originalText = submitButton.innerHTML || submitButton.value;
                    submitButton.disabled = true;
                    submitButton.innerHTML = '<span class="spinner-border spinner-border-sm me-2" role="status" aria-hidden="true"></span>Loading...';
                    submitButton.value = 'Loading...';
                    
                    // Re-enable after 10 seconds in case of error
                    setTimeout(() => {
                        submitButton.disabled = false;
                        submitButton.innerHTML = originalText;
                        submitButton.value = originalText;
                    }, 10000);
                }
            });
        });
    }

    // Enhanced alert animations
    function initAlertAnimations() {
        const alerts = document.querySelectorAll('.alert');
        alerts.forEach(alert => {
            // Add entrance animation
            alert.style.opacity = '0';
            alert.style.transform = 'translateY(-20px)';
            
            setTimeout(() => {
                alert.style.transition = 'all 0.5s ease-out';
                alert.style.opacity = '1';
                alert.style.transform = 'translateY(0)';
            }, 100);
            
            // Auto-dismiss after 5 seconds for success messages
            if (alert.classList.contains('alert-success') && !alert.querySelector('.btn-close').disabled) {
                setTimeout(() => {
                    alert.style.opacity = '0';
                    alert.style.transform = 'translateY(-20px)';
                    setTimeout(() => {
                        if (alert.parentNode) {
                            alert.remove();
                        }
                    }, 500);
                }, 5000);
            }
        });
    }

    // Add smooth scroll to top button
    function initScrollToTop() {
        // Create scroll to top button
        const scrollButton = document.createElement('button');
        scrollButton.innerHTML = '<span class="material-symbols-outlined">keyboard_arrow_up</span>';
        scrollButton.className = 'btn btn-primary rounded-circle position-fixed';
        scrollButton.style.cssText = 'bottom: 20px; right: 20px; width: 50px; height: 50px; z-index: 1000; display: none; box-shadow: 0 4px 12px rgba(0,0,0,0.3); transition: all 0.3s ease;';
        scrollButton.setAttribute('aria-label', 'Scroll to top');
        scrollButton.id = 'scrollToTop';
        document.body.appendChild(scrollButton);

        // Show/hide button on scroll
        window.addEventListener('scroll', function() {
            if (window.pageYOffset > 300) {
                scrollButton.style.display = 'flex';
                scrollButton.style.alignItems = 'center';
                scrollButton.style.justifyContent = 'center';
            } else {
                scrollButton.style.display = 'none';
            }
        });

        // Smooth scroll to top
        scrollButton.addEventListener('click', function() {
            window.scrollTo({
                top: 0,
                behavior: 'smooth'
            });
        });
    }

    // Add confirmation dialogs with better UX
    function initConfirmations() {
        document.querySelectorAll('[data-confirm]').forEach(element => {
            element.addEventListener('click', function(e) {
                const message = this.getAttribute('data-confirm') || 'Are you sure?';
                if (!confirm(message)) {
                    e.preventDefault();
                    e.stopPropagation();
                    return false;
                }
            });
        });
    }

    // Add input focus enhancements
    function initInputEnhancements() {
        document.querySelectorAll('input, textarea, select').forEach(input => {
            // Add focus animation
            input.addEventListener('focus', function() {
                this.parentElement?.classList.add('focused');
            });

            input.addEventListener('blur', function() {
                this.parentElement?.classList.remove('focused');
            });

            // Add character counter for textareas with maxlength
            if (input.tagName === 'TEXTAREA' && input.hasAttribute('maxlength')) {
                const maxLength = parseInt(input.getAttribute('maxlength'));
                const counter = document.createElement('small');
                counter.className = 'text-muted character-counter';
                counter.style.display = 'block';
                counter.style.textAlign = 'right';
                counter.style.marginTop = '0.25rem';
                input.parentElement?.appendChild(counter);

                const updateCounter = () => {
                    const remaining = maxLength - input.value.length;
                    counter.textContent = `${input.value.length} / ${maxLength} characters`;
                    if (remaining < 50) {
                        counter.classList.add('text-warning');
                        counter.classList.remove('text-muted');
                    } else {
                        counter.classList.remove('text-warning');
                        counter.classList.add('text-muted');
                    }
                };

                input.addEventListener('input', updateCounter);
                updateCounter();
            }
        });
    }

    // Add page loading indicator
    function initPageLoader() {
        // Create loader if it doesn't exist
        if (!document.getElementById('page-loader')) {
            const loader = document.createElement('div');
            loader.id = 'page-loader';
            loader.innerHTML = `
                <div class="text-center">
                    <div class="spinner-border text-primary" role="status" style="width: 3rem; height: 3rem;">
                        <span class="visually-hidden">Loading...</span>
                    </div>
                    <p class="mt-3 text-muted">Loading...</p>
                </div>
            `;
            document.body.appendChild(loader);
        }

        // Hide loader when page is loaded
        window.addEventListener('load', function() {
            const loader = document.getElementById('page-loader');
            if (loader) {
                loader.style.opacity = '0';
                setTimeout(() => {
                    loader.style.display = 'none';
                }, 300);
            }
        });
    }

    // Add helpful hints and tips
    function initHelpfulHints() {
        // Add info icons with tooltips for required fields
        document.querySelectorAll('label').forEach(label => {
            if (label.querySelector('input[required], select[required], textarea[required]')) {
                const infoIcon = document.createElement('span');
                infoIcon.className = 'material-symbols-outlined ms-1 text-info';
                infoIcon.style.fontSize = '0.875rem';
                infoIcon.textContent = 'info';
                infoIcon.setAttribute('data-bs-toggle', 'tooltip');
                infoIcon.setAttribute('data-bs-placement', 'right');
                infoIcon.setAttribute('title', 'This field is required');
                label.appendChild(infoIcon);
            }
        });
    }

    // Add success feedback for actions
    function initSuccessFeedback() {
        // Show success toast for successful actions
        const showSuccessToast = (message) => {
            const toast = document.createElement('div');
            toast.className = 'toast show position-fixed top-0 end-0 m-3';
            toast.style.zIndex = '9999';
            toast.innerHTML = `
                <div class="toast-header bg-success text-white">
                    <span class="material-symbols-outlined me-2">check_circle</span>
                    <strong class="me-auto">Success</strong>
                    <button type="button" class="btn-close btn-close-white" data-bs-dismiss="toast"></button>
                </div>
                <div class="toast-body">
                    ${message}
                </div>
            `;
            document.body.appendChild(toast);

            setTimeout(() => {
                toast.remove();
            }, 3000);
        };

        // Listen for success messages
        window.showSuccessToast = showSuccessToast;
    }

    // Initialize all enhancements
    function init() {
        initTooltips();
        initFormLoading();
        initAlertAnimations();
        initScrollToTop();
        initConfirmations();
        initInputEnhancements();
        initPageLoader();
        initHelpfulHints();
        initSuccessFeedback();
    }

    // Run on DOM ready
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }
})();

