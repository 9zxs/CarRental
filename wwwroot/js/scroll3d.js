// 3D 滚动动画效果
(function() {
    'use strict';

    // 检查是否支持 Intersection Observer
    if (!window.IntersectionObserver) {
        console.warn('Intersection Observer not supported, using fallback');
        return;
    }

    // 3D 透视效果配置
    const perspectiveConfig = {
        perspective: 1000, // 透视距离
        rotateX: 15, // X轴旋转角度
        rotateY: 15, // Y轴旋转角度
        translateZ: 50 // Z轴平移距离
    };

    // 创建 3D 滚动动画观察器
    const observerOptions = {
        root: null,
        rootMargin: '0px',
        threshold: [0, 0.25, 0.5, 0.75, 1]
    };

    // 处理 3D 滚动动画
    function handle3DScroll(entries, observer) {
        entries.forEach(entry => {
            const element = entry.target;
            const ratio = entry.intersectionRatio;
            const rect = entry.boundingClientRect;
            const viewportHeight = window.innerHeight;
            
            // 计算元素在视口中的位置
            const elementTop = rect.top;
            const elementCenter = elementTop + rect.height / 2;
            const viewportCenter = viewportHeight / 2;
            
            // 计算距离中心点的距离（归一化到 -1 到 1）
            const distanceFromCenter = (elementCenter - viewportCenter) / viewportHeight;
            
            // 计算滚动进度（0 到 1）
            const scrollProgress = 1 - Math.abs(distanceFromCenter);
            
            // 获取自定义属性
            const scrollSpeed = parseFloat(element.dataset.scrollSpeed || '0.5');
            const scrollDepth = parseFloat(element.dataset.scrollDepth || '50');
            const scrollDirection = element.dataset.scrollDirection || 'both';
            
            // 计算旋转和平移值
            let rotateX = 0;
            let rotateY = 0;
            let translateZ = 0;
            let opacity = 0.5 + (scrollProgress * 0.5);
            
            if (scrollDirection === 'x' || scrollDirection === 'both') {
                rotateY = distanceFromCenter * perspectiveConfig.rotateY * scrollSpeed;
            }
            if (scrollDirection === 'y' || scrollDirection === 'both') {
                rotateX = -distanceFromCenter * perspectiveConfig.rotateX * scrollSpeed;
            }
            
            translateZ = scrollProgress * scrollDepth * scrollSpeed;
            
            // 应用 3D 变换
            const transform = `
                perspective(${perspectiveConfig.perspective}px)
                rotateX(${rotateX}deg)
                rotateY(${rotateY}deg)
                translateZ(${translateZ}px)
            `;
            
            element.style.transform = transform;
            element.style.opacity = opacity;
            element.style.transition = 'transform 0.3s ease-out, opacity 0.3s ease-out';
        });
    }

    // 创建观察器
    const observer = new IntersectionObserver(handle3DScroll, observerOptions);

    // 初始化所有带有 data-scroll-3d 属性的元素
    function init3DScroll() {
        const elements = document.querySelectorAll('[data-scroll-3d]');
        elements.forEach(element => {
            // 设置初始样式
            element.style.transformStyle = 'preserve-3d';
            element.style.willChange = 'transform';
            
            // 观察元素
            observer.observe(element);
        });
    }

    // 鼠标移动时的 3D 倾斜效果（用于卡片）
    function initCard3DTilt() {
        const cards = document.querySelectorAll('.card-3d');
        cards.forEach(card => {
            card.addEventListener('mousemove', function(e) {
                const rect = card.getBoundingClientRect();
                const x = e.clientX - rect.left;
                const y = e.clientY - rect.top;
                
                const centerX = rect.width / 2;
                const centerY = rect.height / 2;
                
                const rotateX = ((y - centerY) / centerY) * -10;
                const rotateY = ((x - centerX) / centerX) * 10;
                
                card.style.transform = `
                    perspective(1000px)
                    rotateX(${rotateX}deg)
                    rotateY(${rotateY}deg)
                    translateZ(20px)
                `;
            });
            
            card.addEventListener('mouseleave', function() {
                card.style.transform = 'perspective(1000px) rotateX(0) rotateY(0) translateZ(0)';
            });
        });
    }

    // 页面滚动时的视差效果
    function initParallaxScroll() {
        const parallaxElements = document.querySelectorAll('[data-parallax]');
        
        window.addEventListener('scroll', () => {
            const scrolled = window.pageYOffset;
            
            parallaxElements.forEach(element => {
                const speed = parseFloat(element.dataset.parallax || '0.5');
                const yPos = -(scrolled * speed);
                
                element.style.transform = `translateY(${yPos}px)`;
            });
        });
    }

    // 滚动时的淡入淡出效果
    function initFadeOnScroll() {
        const fadeElements = document.querySelectorAll('[data-fade-scroll]');
        
        const fadeObserver = new IntersectionObserver((entries) => {
            entries.forEach(entry => {
                if (entry.isIntersecting) {
                    entry.target.style.opacity = '1';
                    entry.target.style.transform = 'translateY(0)';
                } else {
                    const direction = entry.target.dataset.fadeDirection || 'up';
                    if (direction === 'up') {
                        entry.target.style.opacity = '0';
                        entry.target.style.transform = 'translateY(50px)';
                    }
                }
            });
        }, {
            threshold: 0.1
        });
        
        fadeElements.forEach(element => {
            element.style.opacity = '0';
            element.style.transition = 'opacity 0.6s ease-out, transform 0.6s ease-out';
            const direction = element.dataset.fadeDirection || 'up';
            if (direction === 'up') {
                element.style.transform = 'translateY(50px)';
            }
            fadeObserver.observe(element);
        });
    }

    // 页面加载完成后初始化
    function initializeAll() {
        try {
            init3DScroll();
            initCard3DTilt();
            initParallaxScroll();
            initFadeOnScroll();
            
            // 添加更多3D效果到卡片
            const cards = document.querySelectorAll('.card:not(.card-3d):not([data-scroll-3d])');
            cards.forEach((card, index) => {
                if (!card.closest('[data-scroll-3d]')) {
                    card.classList.add('card-3d');
                    card.setAttribute('data-fade-scroll', '');
                    card.style.opacity = '0';
                }
            });
        } catch (error) {
            console.error('Error initializing 3D scroll effects:', error);
        }
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', () => {
            setTimeout(initializeAll, 100);
        });
    } else {
        setTimeout(initializeAll, 100);
    }

    // 当页面内容动态加载后重新初始化
    window.addEventListener('load', () => {
        setTimeout(initializeAll, 200);
    });

    // 监听DOM变化，动态添加新元素的3D效果
    if (window.MutationObserver) {
        const observer = new MutationObserver(() => {
            init3DScroll();
            initCard3DTilt();
            initFadeOnScroll();
        });
        
        observer.observe(document.body, {
            childList: true,
            subtree: true
        });
    }

    // 暴露全局函数以便手动触发
    window.scroll3D = {
        init: init3DScroll,
        initCardTilt: initCard3DTilt,
        initParallax: initParallaxScroll,
        initFade: initFadeOnScroll
    };
})();
