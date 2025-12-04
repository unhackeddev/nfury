class NFuryApp {
    constructor() {
        this.connection = null;
        this.isRunning = false;
        this.testId = null;
        this.charts = {};
        this.statusCodeCounts = {};
        this.responseTimeHistory = [];
        this.rpsHistory = [];
        this.maxDataPoints = 50;
        this.peakRps = 0;
        this.startTime = null;
        this.projects = [];
        this.selectedProjectId = null;
        this.selectedEndpointId = null;
        this.expandedProjects = new Set();
        this.executions = [];
        
        this.init();
    }

    getFullUrl(schemaId, urlId) {
        const schema = document.getElementById(schemaId).value;
        const url = document.getElementById(urlId).value;
        return schema + url;
    }

    setUrlWithSchema(schemaId, urlId, fullUrl) {
        if (!fullUrl) {
            document.getElementById(schemaId).value = 'https://';
            document.getElementById(urlId).value = '';
            return;
        }
        if (fullUrl.startsWith('https://')) {
            document.getElementById(schemaId).value = 'https://';
            document.getElementById(urlId).value = fullUrl.substring(8);
        } else if (fullUrl.startsWith('http://')) {
            document.getElementById(schemaId).value = 'http://';
            document.getElementById(urlId).value = fullUrl.substring(7);
        } else {
            document.getElementById(schemaId).value = 'https://';
            document.getElementById(urlId).value = fullUrl;
        }
    }

    async init() {
        this.initCharts();
        this.bindEvents();
        this.initTooltips();
        await this.connectSignalR();
        await this.loadProjects();
        await this.loadStatistics();
        this.updateDate();
    }

    initTooltips() {
        document.querySelectorAll('.info-tooltip').forEach(tooltip => {
            tooltip.addEventListener('mouseenter', () => {
                const rect = tooltip.getBoundingClientRect();
                const tooltipWidth = 280;
                const tooltipHeight = 100;
                
                let left = rect.left + (rect.width / 2) - (tooltipWidth / 2);
                let top = rect.top - tooltipHeight - 15;
                
                if (left < 10) left = 10;
                if (left + tooltipWidth > window.innerWidth - 10) {
                    left = window.innerWidth - tooltipWidth - 10;
                }
                
                if (top < 10) {
                    top = rect.bottom + 15;
                    tooltip.classList.add('tooltip-below');
                } else {
                    tooltip.classList.remove('tooltip-below');
                }
                
                tooltip.style.setProperty('--tooltip-left', `${left}px`);
                tooltip.style.setProperty('--tooltip-top', `${top}px`);
                tooltip.style.setProperty('--arrow-left', `${rect.left + rect.width / 2}px`);
                tooltip.style.setProperty('--arrow-top', `${rect.top - 8}px`);
            });
        });
    }

    updateDate() {
        const now = new Date();
        document.getElementById('infoDate').textContent = now.toLocaleDateString('en-US', {
            day: 'numeric',
            month: 'short',
            year: 'numeric',
            hour: '2-digit',
            minute: '2-digit'
        });
    }

    async loadProjects() {
        try {
            const response = await fetch('/api/projects');
            this.projects = await response.json();
            
            for (const project of this.projects) {
                try {
                    const endpointsRes = await fetch(`/api/projects/${project.id}/endpoints`);
                    project.endpoints = await endpointsRes.json();
                } catch {
                    project.endpoints = [];
                }
            }
            
            this.renderProjectList();
        } catch (err) {
            console.error('Failed to load projects:', err);
        }
    }

    renderProjectList() {
        const container = document.getElementById('projectTree');
        
        let html = '';

        for (const project of this.projects) {
            const isExpanded = this.expandedProjects.has(project.id);
            const endpoints = project.endpoints || [];
            const hasAuth = project.authUrl ? true : false;
            
            html += `
                <div class="project-container ${isExpanded ? 'expanded' : ''}" data-project-id="${project.id}">
                    <div class="project-header ${isExpanded ? 'expanded' : ''}" onclick="window.app.toggleProject(${project.id})">
                        <i class="fas fa-chevron-right chevron"></i>
                        <i class="fas fa-folder${isExpanded ? '-open' : ''} folder-icon"></i>
                        <span class="project-name">${this.escapeHtml(project.name)}</span>
                        ${hasAuth ? '<i class="fas fa-key project-auth-icon" title="Has authentication configured"></i>' : ''}
                        <div class="project-actions">
                            <button onclick="event.stopPropagation(); exportProject(${project.id}, '${this.escapeHtml(project.name).replace(/'/g, "\\'")}')" class="export" title="Export project">
                                <i class="fas fa-file-export"></i>
                            </button>
                            <button onclick="event.stopPropagation(); window.app.showProjectAuthModal(${project.id})" class="auth-btn ${hasAuth ? 'has-auth' : ''}" title="${hasAuth ? 'Edit' : 'Add'} authentication endpoint">
                                <i class="fas fa-key"></i>
                            </button>
                            <button onclick="event.stopPropagation(); window.app.showAddEndpointModal(${project.id})" class="add-endpoint" title="Add endpoint">
                                <i class="fas fa-plus"></i>
                            </button>
                            <button onclick="event.stopPropagation(); window.app.deleteProject(${project.id})" class="delete" title="Delete project">
                                <i class="fas fa-trash"></i>
                            </button>
                        </div>
                    </div>
                    <div class="endpoint-list">
                        ${endpoints.length === 0 ? `
                            <div class="endpoint-empty">
                                No endpoints. <a onclick="window.app.showAddEndpointModal(${project.id})">Add one</a>
                            </div>
                        ` : endpoints.map(ep => `
                            <div class="endpoint-item ${this.selectedEndpointId === ep.id ? 'active' : ''}" 
                                 onclick="window.app.selectEndpoint(${ep.id})"
                                 data-endpoint-id="${ep.id}">
                                <span class="method-badge ${ep.method.toLowerCase()}">${ep.method}</span>
                                <span class="endpoint-name">${this.escapeHtml(ep.name)}</span>
                                ${ep.requiresAuth ? '<i class="fas fa-lock endpoint-auth-icon" title="Requires authentication"></i>' : ''}
                                <div class="endpoint-actions">
                                    <button onclick="event.stopPropagation(); window.app.runEndpointTest(${ep.id})" class="play" title="Run test">
                                        <i class="fas fa-play"></i>
                                    </button>
                                    <button onclick="event.stopPropagation(); window.app.toggleEndpointHistory(${ep.id})" class="history" title="History">
                                        <i class="fas fa-history"></i>
                                    </button>
                                    <button onclick="event.stopPropagation(); window.app.showEditEndpointModal(${ep.id})" title="Edit">
                                        <i class="fas fa-pen"></i>
                                    </button>
                                    <button onclick="event.stopPropagation(); window.app.deleteEndpoint(${ep.id}, ${project.id})" class="delete" title="Delete">
                                        <i class="fas fa-trash"></i>
                                    </button>
                                </div>
                            </div>
                            <div class="endpoint-history" id="endpointHistory-${ep.id}" style="display: none;"></div>
                        `).join('')}
                    </div>
                </div>
            `;
        }

        container.innerHTML = html;
    }

    toggleProject(projectId) {
        if (this.expandedProjects.has(projectId)) {
            this.expandedProjects.delete(projectId);
        } else {
            this.expandedProjects.add(projectId);
        }
        this.renderProjectList();
    }

    async selectEndpoint(endpointId) {
        this.selectedEndpointId = endpointId;
        this.selectedProjectId = null;
        this.renderProjectList();
        
        try {
            const response = await fetch(`/api/endpoints/${endpointId}`);
            const endpoint = await response.json();
            
            document.getElementById('projectNameHeader').textContent = endpoint.name;
            
            this.setUrlWithSchema('urlSchema', 'url', endpoint.url);
            document.getElementById('method').value = endpoint.method || 'GET';
            document.getElementById('users').value = endpoint.users || 10;
            document.getElementById('requests').value = endpoint.requests || 100;
            document.getElementById('duration').value = endpoint.duration || '';
            document.getElementById('contentType').value = endpoint.contentType || 'application/json';
            document.getElementById('body').value = endpoint.body || '';
            document.getElementById('insecure').checked = endpoint.insecure || false;
            
            const container = document.getElementById('headersContainer');
            container.innerHTML = '';
            if (endpoint.headersJson) {
                const headers = JSON.parse(endpoint.headersJson);
                for (const [key, value] of Object.entries(headers)) {
                    addHeader();
                    const rows = container.querySelectorAll('.header-row');
                    const lastRow = rows[rows.length - 1];
                    lastRow.querySelector('.header-key').value = key;
                    lastRow.querySelector('.header-value').value = value;
                }
            }
            
            if (endpoint.authenticationJson) {
                const auth = JSON.parse(endpoint.authenticationJson);
                document.getElementById('useAuth').checked = true;
                toggleAuthSection();
                document.getElementById('authUrl').value = auth.url || '';
                document.getElementById('authMethod').value = auth.method || 'POST';
                document.getElementById('authBody').value = auth.body || '';
                document.getElementById('authContentType').value = auth.contentType || 'application/json';
                document.getElementById('tokenPath').value = auth.tokenPath || 'access_token';
                document.getElementById('headerName').value = auth.headerName || 'Authorization';
                document.getElementById('headerPrefix').value = auth.headerPrefix || 'Bearer ';
            } else {
                document.getElementById('useAuth').checked = false;
                toggleAuthSection();
            }
        } catch (err) {
            console.error('Failed to load endpoint:', err);
        }
    }

    showAddEndpointModal(projectId) {
        this.editingEndpointProjectId = projectId;
        this.editingEndpointId = null;
        
        document.getElementById('endpointName').value = '';
        document.getElementById('endpointUrlSchema').value = 'https://';
        document.getElementById('endpointUrl').value = '';
        document.getElementById('endpointMethod').value = 'GET';
        document.getElementById('endpointUsers').value = '10';
        document.getElementById('endpointRequests').value = '100';
        document.getElementById('endpointDuration').value = '';
        document.getElementById('endpointContentType').value = 'application/json';
        document.getElementById('endpointBody').value = '';
        document.getElementById('endpointInsecure').checked = false;
        document.getElementById('endpointRequiresAuth').checked = false;
        document.getElementById('endpointHeadersContainer').innerHTML = '';
        
        document.getElementById('endpointModalTitle').textContent = 'Add Endpoint';
        document.getElementById('endpointModal').classList.add('open');
        document.getElementById('overlay').classList.add('visible');
    }

    async showEditEndpointModal(endpointId) {
        try {
            const response = await fetch(`/api/endpoints/${endpointId}`);
            const endpoint = await response.json();
            
            this.editingEndpointId = endpointId;
            this.editingEndpointProjectId = endpoint.projectId;
            
            document.getElementById('endpointName').value = endpoint.name || '';
            this.setUrlWithSchema('endpointUrlSchema', 'endpointUrl', endpoint.url);
            document.getElementById('endpointMethod').value = endpoint.method || 'GET';
            document.getElementById('endpointUsers').value = endpoint.users || 10;
            document.getElementById('endpointRequests').value = endpoint.requests || 100;
            document.getElementById('endpointDuration').value = endpoint.duration || '';
            document.getElementById('endpointContentType').value = endpoint.contentType || 'application/json';
            document.getElementById('endpointBody').value = endpoint.body || '';
            document.getElementById('endpointInsecure').checked = endpoint.insecure || false;
            document.getElementById('endpointRequiresAuth').checked = endpoint.requiresAuth || false;
            
            const headersContainer = document.getElementById('endpointHeadersContainer');
            headersContainer.innerHTML = '';
            if (endpoint.headers && typeof endpoint.headers === 'object') {
                Object.entries(endpoint.headers).forEach(([key, value]) => {
                    addEndpointHeader(key, value);
                });
            }
            
            document.getElementById('endpointModalTitle').textContent = 'Edit Endpoint';
            document.getElementById('endpointModal').classList.add('open');
            document.getElementById('overlay').classList.add('visible');
        } catch (err) {
            console.error('Failed to load endpoint:', err);
            alert('Failed to load endpoint');
        }
    }

    async saveEndpoint() {
        const name = document.getElementById('endpointName').value;
        const url = this.getFullUrl('endpointUrlSchema', 'endpointUrl');
        const method = document.getElementById('endpointMethod').value;
        const users = parseInt(document.getElementById('endpointUsers').value) || 10;
        const requests = parseInt(document.getElementById('endpointRequests').value) || 100;
        const duration = parseInt(document.getElementById('endpointDuration').value) || null;
        const contentType = document.getElementById('endpointContentType').value;
        const body = document.getElementById('endpointBody').value || null;
        const insecure = document.getElementById('endpointInsecure').checked;
        const requiresAuth = document.getElementById('endpointRequiresAuth').checked;
        
        const headers = {};
        document.querySelectorAll('#endpointHeadersContainer .header-row').forEach(row => {
            const key = row.querySelector('.header-key')?.value;
            const value = row.querySelector('.header-value')?.value;
            if (key && value) {
                headers[key] = value;
            }
        });
        
        if (!name || !url) {
            alert('Please fill in required fields (Name and URL)');
            return;
        }
        
        const dto = {
            name,
            url,
            method,
            users,
            requests,
            duration,
            contentType,
            body,
            insecure,
            requiresAuth,
            headers: Object.keys(headers).length > 0 ? headers : null
        };
        
        try {
            let response;
            if (this.editingEndpointId) {
                response = await fetch(`/api/endpoints/${this.editingEndpointId}`, {
                    method: 'PUT',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify(dto)
                });
            } else {
                response = await fetch(`/api/projects/${this.editingEndpointProjectId}/endpoints`, {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify(dto)
                });
            }
            
            if (response.ok) {
                closeEndpointModal();
                this.expandedProjects.add(this.editingEndpointProjectId);
                await this.loadProjects();
            } else {
                throw new Error('Failed to save endpoint');
            }
        } catch (err) {
            console.error('Failed to save endpoint:', err);
            alert('Failed to save endpoint: ' + err.message);
        }
    }

    async deleteEndpoint(endpointId, _projectId) {
        if (!confirm('Are you sure you want to delete this endpoint?')) return;
        
        try {
            await fetch(`/api/endpoints/${endpointId}`, { method: 'DELETE' });
            if (this.selectedEndpointId === endpointId) {
                this.selectedEndpointId = null;
                document.getElementById('projectNameHeader').textContent = 'Quick Test';
                selectQuickTest();
            }
            await this.loadProjects();
        } catch (err) {
            console.error('Failed to delete endpoint:', err);
        }
    }

    async runEndpointTest(endpointId) {
        if (this.isRunning) {
            alert('A test is already running');
            return;
        }

        try {
            this.resetCharts();
            this.toggleButtons(true);
            this.updateTestStatus('running');
            this.startTime = new Date();
            this.peakRps = 0;

            const response = await fetch(`/api/endpoints/${endpointId}/test/start`, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({})
            });

            if (response.ok) {
                const data = await response.json();
                this.testId = data.testId;
                this.isRunning = true;
                this.selectedEndpointId = endpointId;
                this.renderProjectList();
                await this.loadStatistics();
            } else {
                const error = await response.json();
                throw new Error(error.error || 'Failed to start test');
            }
        } catch (err) {
            console.error('Error starting test:', err);
            alert('Failed to start test: ' + err.message);
            this.toggleButtons(false);
            this.updateTestStatus('idle');
        }
    }

    async loadEndpointHistory(endpointId) {
        try {
            const response = await fetch(`/api/endpoints/${endpointId}/executions?page=1&pageSize=5`);
            const data = await response.json();
            return data.executions || [];
        } catch (err) {
            console.error('Failed to load endpoint history:', err);
            return [];
        }
    }

    async toggleEndpointHistory(endpointId) {
        const historyEl = document.getElementById(`endpointHistory-${endpointId}`);
        if (!historyEl) return;

        if (historyEl.style.display === 'none') {
            historyEl.style.display = 'block';
            historyEl.innerHTML = '<div class="history-loading"><i class="fas fa-spinner fa-spin"></i> Loading...</div>';
            
            const executions = await this.loadEndpointHistory(endpointId);
            
            if (executions.length === 0) {
                historyEl.innerHTML = '<div class="history-empty-small">No test history</div>';
            } else {
                historyEl.innerHTML = executions.map(exec => {
                    const date = new Date(exec.startedAt);
                    const statusClass = exec.status.toLowerCase();
                    return `
                        <div class="history-item-mini" onclick="event.stopPropagation(); window.app.showExecutionDetails(${exec.id})">
                            <span class="history-status ${statusClass}">
                                <i class="fas fa-${statusClass === 'completed' ? 'check' : statusClass === 'failed' ? 'times' : 'circle'}"></i>
                            </span>
                            <span class="history-date">${date.toLocaleDateString()} ${date.toLocaleTimeString([], {hour: '2-digit', minute:'2-digit'})}</span>
                            <span class="history-rps">${exec.requestsPerSecond.toFixed(2)} rps</span>
                        </div>
                    `;
                }).join('');
            }
        } else {
            historyEl.style.display = 'none';
        }
    }

    async loadStatistics() {
        try {
            const response = await fetch('/api/executions/statistics');
            const stats = await response.json();
            document.getElementById('totalExecutions').textContent = stats.totalExecutions;
        } catch (err) {
            console.error('Failed to load statistics:', err);
        }
    }

    async createProject(dto) {
        try {
            const response = await fetch('/api/projects', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify(dto)
            });
            
            if (response.ok) {
                const project = await response.json();
                this.expandedProjects.add(project.id);
                await this.loadProjects();
                return project;
            } else {
                throw new Error('Failed to create project');
            }
        } catch (err) {
            console.error('Failed to create project:', err);
            alert('Failed to create project: ' + err.message);
            return null;
        }
    }

    async deleteProject(projectId) {
        if (!confirm('Are you sure you want to delete this project?')) return;
        
        try {
            await fetch(`/api/projects/${projectId}`, { method: 'DELETE' });
            if (this.selectedProjectId === projectId) {
                this.selectedProjectId = null;
                document.getElementById('projectNameHeader').textContent = 'Quick Test';
            }
            await this.loadProjects();
        } catch (err) {
            console.error('Failed to delete project:', err);
        }
    }
    
    async showProjectAuthModal(projectId) {
        try {
            const response = await fetch(`/api/projects/${projectId}`);
            const project = await response.json();
            
            this.editingProjectAuthId = projectId;
            
            this.setUrlWithSchema('projectAuthUrlSchema', 'projectAuthUrl', project.authUrl || '');
            document.getElementById('projectAuthMethod').value = project.authMethod || 'POST';
            document.getElementById('projectAuthContentType').value = project.authContentType || 'application/json';
            document.getElementById('projectAuthBody').value = project.authBody || '';
            document.getElementById('projectAuthTokenPath').value = project.authTokenPath || '$.token';
            document.getElementById('projectAuthHeaderName').value = project.authHeaderName || 'Authorization';
            document.getElementById('projectAuthHeaderPrefix').value = project.authHeaderPrefix || 'Bearer';
            
            const headersContainer = document.getElementById('projectAuthHeadersContainer');
            headersContainer.innerHTML = '';
            if (project.authHeadersJson) {
                try {
                    const headers = JSON.parse(project.authHeadersJson);
                    Object.entries(headers).forEach(([key, value]) => {
                        addProjectAuthHeader(key, value);
                    });
                } catch (e) {
                    console.error('Failed to parse auth headers:', e);
                }
            }
            
            document.getElementById('projectAuthModalTitle').textContent = project.authUrl ? 'Edit Authentication Endpoint' : 'Add Authentication Endpoint';
            document.getElementById('projectAuthModal').classList.add('open');
            document.getElementById('overlay').classList.add('visible');
        } catch (err) {
            console.error('Failed to load project:', err);
            alert('Failed to load project');
        }
    }
    
    async saveProjectAuth() {
        const url = this.getFullUrl('projectAuthUrlSchema', 'projectAuthUrl');
        const method = document.getElementById('projectAuthMethod').value;
        const contentType = document.getElementById('projectAuthContentType').value;
        const body = document.getElementById('projectAuthBody').value || null;
        const tokenPath = document.getElementById('projectAuthTokenPath').value || '$.token';
        const headerName = document.getElementById('projectAuthHeaderName').value || 'Authorization';
        const headerPrefix = document.getElementById('projectAuthHeaderPrefix').value || 'Bearer';
        
        const headers = {};
        document.querySelectorAll('#projectAuthHeadersContainer .header-row').forEach(row => {
            const key = row.querySelector('.header-key')?.value;
            const value = row.querySelector('.header-value')?.value;
            if (key && value) {
                headers[key] = value;
            }
        });
        
        if (!url) {
            alert('Please enter the authentication URL');
            return;
        }
        
        const dto = {
            authUrl: url,
            authMethod: method,
            authContentType: contentType,
            authBody: body,
            authHeaders: Object.keys(headers).length > 0 ? headers : null,
            authTokenPath: tokenPath,
            authHeaderName: headerName,
            authHeaderPrefix: headerPrefix
        };
        
        try {
            const response = await fetch(`/api/projects/${this.editingProjectAuthId}/auth`, {
                method: 'PUT',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify(dto)
            });
            
            if (response.ok) {
                closeAllModals();
                await this.loadProjects();
            } else {
                throw new Error('Failed to save authentication configuration');
            }
        } catch (err) {
            console.error('Failed to save auth config:', err);
            alert('Failed to save authentication configuration: ' + err.message);
        }
    }
    
    async deleteProjectAuth() {
        if (!confirm('Are you sure you want to remove the authentication endpoint?')) return;
        
        try {
            const response = await fetch(`/api/projects/${this.editingProjectAuthId}/auth`, {
                method: 'DELETE'
            });
            
            if (response.ok) {
                closeAllModals();
                await this.loadProjects();
            } else {
                throw new Error('Failed to delete authentication configuration');
            }
        } catch (err) {
            console.error('Failed to delete auth config:', err);
            alert('Failed to delete authentication configuration: ' + err.message);
        }
    }

    async loadHistory(status = '') {
        try {
            const url = this.selectedProjectId 
                ? `/api/projects/${this.selectedProjectId}/executions?page=1&pageSize=50`
                : '/api/executions';
            const response = await fetch(url);
            const data = await response.json();
            this.executions = data.executions || data;
            this.renderHistory(status);
        } catch (err) {
            console.error('Failed to load history:', err);
        }
    }

    renderHistory(statusFilter = '') {
        const container = document.getElementById('historyList');
        let executions = this.executions;
        
        if (statusFilter) {
            executions = executions.filter(e => e.status === statusFilter);
        }
        
        if (executions.length === 0) {
            container.innerHTML = `
                <div class="history-empty">
                    <i class="fas fa-inbox"></i>
                    <p>No test history yet</p>
                </div>
            `;
            return;
        }
        
        container.innerHTML = '';
        for (const exec of executions) {
            const div = document.createElement('div');
            div.className = 'history-item';
            div.onclick = () => this.showExecutionDetails(exec.id);
            
            const date = new Date(exec.startedAt);
            const statusClass = exec.status.toLowerCase();
            
            div.innerHTML = `
                <div class="history-item-header">
                    <span class="history-item-status ${statusClass}">
                        <i class="fas fa-${statusClass === 'completed' ? 'check' : statusClass === 'failed' ? 'times' : 'circle'}"></i>
                        ${exec.status}
                    </span>
                    <span class="history-item-date">${date.toLocaleDateString()} ${date.toLocaleTimeString()}</span>
                </div>
                <div class="history-item-url">${this.escapeHtml(exec.url)}</div>
                <div class="history-item-stats">
                    <span class="history-item-stat"><i class="fas fa-exchange-alt"></i> ${exec.totalRequests.toLocaleString()} reqs</span>
                    <span class="history-item-stat"><i class="fas fa-clock"></i> ${exec.averageResponseTime.toFixed(0)}ms avg</span>
                    <span class="history-item-stat"><i class="fas fa-tachometer-alt"></i> ${exec.requestsPerSecond.toFixed(2)} rps</span>
                </div>
            `;
            container.appendChild(div);
        }
    }

    async showExecutionDetails(executionId) {
        try {
            const response = await fetch(`/api/executions/${executionId}/metrics`);
            const execution = await response.json();
            
            let statusCodes = {};
            if (execution.statusCodesJson) {
                try {
                    statusCodes = JSON.parse(execution.statusCodesJson);
                } catch (e) {
                    console.error('Failed to parse statusCodesJson:', e);
                }
            }
            
            const result = {
                testId: execution.testId,
                totalRequests: execution.totalRequests || 0,
                successfulRequests: execution.successfulRequests || 0,
                failedRequests: execution.failedRequests || 0,
                totalElapsedTime: execution.totalElapsedTime || 0,
                requestsPerSecond: execution.requestsPerSecond || 0,
                averageResponseTime: execution.averageResponseTime || 0,
                minResponseTime: execution.minResponseTime || 0,
                maxResponseTime: execution.maxResponseTime || 0,
                percentile50: execution.percentile50 || 0,
                percentile75: execution.percentile75 || 0,
                percentile90: execution.percentile90 || 0,
                percentile95: execution.percentile95 || 0,
                percentile99: execution.percentile99 || 0,
                statusCodes: statusCodes
            };
            
            const headerText = execution.endpoint 
                ? `${execution.endpoint.name} - Execution ${new Date(execution.startedAt).toLocaleString()}`
                : `Execution ${new Date(execution.startedAt).toLocaleString()}`;
            document.getElementById('projectNameHeader').textContent = headerText;
            
            this.updateTestStatus(execution.status.toLowerCase());
            
            document.getElementById('statTotalRequests').textContent = this.formatNumber(result.totalRequests);
            document.getElementById('statFailed').textContent = this.formatNumber(result.failedRequests);
            document.getElementById('statRps').textContent = result.requestsPerSecond.toFixed(2);
            document.getElementById('statAvgResponse').textContent = Math.round(result.averageResponseTime);
            
            document.getElementById('avgResponseBottom').textContent = Math.round(result.averageResponseTime) + 'ms';
            document.getElementById('totalRequestsBottom').textContent = this.formatNumber(result.totalRequests);
            document.getElementById('rpsBottom').textContent = result.requestsPerSecond.toFixed(2);
            
            const pctChart = this.charts.percentile;
            pctChart.data.datasets[0].data = [
                result.percentile50,
                result.percentile75,
                result.percentile90,
                result.percentile95,
                result.percentile99
            ];
            pctChart.update();
            
            if (execution.metrics && execution.metrics.length > 0) {
                const rtChart = this.charts.responseTime;
                rtChart.data.labels = execution.metrics.map((_, i) => i);
                rtChart.data.datasets[0].data = execution.metrics.map(m => m.responseTime);
                rtChart.data.datasets[1].data = execution.metrics.map(m => m.averageResponseTime);
                rtChart.update();
                
                const rpsChart = this.charts.rps;
                rpsChart.data.labels = execution.metrics.map((_, i) => i);
                rpsChart.data.datasets[0].data = execution.metrics.map(m => m.currentRps);
                rpsChart.update();
            }
            
            if (Object.keys(statusCodes).length > 0) {
                const scChart = this.charts.statusCodes;
                const labels = Object.keys(statusCodes);
                const data = labels.map(code => statusCodes[code].count || 0);
                
                scChart.data.labels = labels;
                scChart.data.datasets[0].data = data;
                scChart.update();
            }
            
            this.displayFinalResults(result);
            
        } catch (err) {
            console.error('Failed to load execution details:', err);
        }
    }

    escapeHtml(text) {
        const div = document.createElement('div');
        div.textContent = text || '';
        return div.innerHTML;
    }

    initCharts() {
        const rtCtx = document.getElementById('responseTimeChart').getContext('2d');
        
        const gradientPurple = rtCtx.createLinearGradient(0, 0, 0, 280);
        gradientPurple.addColorStop(0, 'rgba(124, 58, 237, 0.3)');
        gradientPurple.addColorStop(1, 'rgba(124, 58, 237, 0.0)');
        
        const gradientCyan = rtCtx.createLinearGradient(0, 0, 0, 280);
        gradientCyan.addColorStop(0, 'rgba(6, 182, 212, 0.3)');
        gradientCyan.addColorStop(1, 'rgba(6, 182, 212, 0.0)');

        this.charts.responseTime = new Chart(rtCtx, {
            type: 'line',
            data: {
                labels: [],
                datasets: [{
                    label: 'Response Time (ms)',
                    data: [],
                    borderColor: '#7c3aed',
                    backgroundColor: gradientPurple,
                    fill: true,
                    tension: 0.4,
                    pointRadius: 0,
                    borderWidth: 2
                }, {
                    label: 'Average (ms)',
                    data: [],
                    borderColor: '#06b6d4',
                    backgroundColor: gradientCyan,
                    fill: true,
                    tension: 0.4,
                    pointRadius: 0,
                    borderWidth: 2
                }]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                animation: { duration: 0 },
                interaction: {
                    intersect: false,
                    mode: 'index'
                },
                scales: {
                    x: {
                        display: true,
                        grid: { display: false },
                        ticks: { 
                            color: '#9ca3af',
                            font: { size: 10 },
                            maxTicksLimit: 8
                        }
                    },
                    y: {
                        display: true,
                        beginAtZero: true,
                        grid: { 
                            color: 'rgba(0,0,0,0.05)',
                            drawBorder: false
                        },
                        ticks: {
                            color: '#9ca3af',
                            font: { size: 10 },
                            callback: (value) => value + 'ms'
                        }
                    }
                },
                plugins: {
                    legend: { 
                        display: true,
                        position: 'top',
                        align: 'end',
                        labels: {
                            boxWidth: 12,
                            padding: 16,
                            font: { size: 11 }
                        }
                    }
                }
            }
        });

        const scCtx = document.getElementById('statusCodeChart').getContext('2d');
        this.charts.statusCode = new Chart(scCtx, {
            type: 'doughnut',
            data: {
                labels: [],
                datasets: [{
                    data: [],
                    backgroundColor: [
                        '#10b981', // 2xx
                        '#f59e0b', // 3xx
                        '#ef4444', // 4xx
                        '#8b5cf6', // 5xx
                        '#6b7280'  // Other
                    ],
                    borderWidth: 0,
                    spacing: 2
                }]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                animation: { duration: 0 },
                cutout: '65%',
                plugins: {
                    legend: { 
                        position: 'right',
                        labels: {
                            boxWidth: 12,
                            padding: 12,
                            font: { size: 11 }
                        }
                    }
                }
            }
        });

        const pctCtx = document.getElementById('percentileChart').getContext('2d');
        this.charts.percentile = new Chart(pctCtx, {
            type: 'bar',
            data: {
                labels: ['P50', 'P75', 'P90', 'P95', 'P99'],
                datasets: [{
                    label: 'Response Time (ms)',
                    data: [0, 0, 0, 0, 0],
                    backgroundColor: [
                        'rgba(16, 185, 129, 0.8)',
                        'rgba(6, 182, 212, 0.8)',
                        'rgba(245, 158, 11, 0.8)',
                        'rgba(239, 68, 68, 0.8)',
                        'rgba(139, 92, 246, 0.8)'
                    ],
                    borderRadius: 6,
                    borderWidth: 0
                }]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                indexAxis: 'y',
                animation: { duration: 0 },
                scales: {
                    x: {
                        beginAtZero: true,
                        grid: { 
                            color: 'rgba(0,0,0,0.05)',
                            drawBorder: false
                        },
                        ticks: {
                            color: '#9ca3af',
                            font: { size: 10 },
                            callback: (value) => value + 'ms'
                        }
                    },
                    y: {
                        grid: { display: false },
                        ticks: {
                            color: '#6b7280',
                            font: { size: 11, weight: '500' }
                        }
                    }
                },
                plugins: {
                    legend: { display: false }
                }
            }
        });
    }

    bindEvents() {
        const form = document.getElementById('testForm');
        form.addEventListener('submit', (e) => {
            e.preventDefault();
            this.startTest();
        });

        document.getElementById('stopBtn').addEventListener('click', () => {
            this.stopTest();
        });

        document.getElementById('method').addEventListener('change', (e) => {
            const bodySection = document.getElementById('bodySection');
            const needsBody = ['POST', 'PUT', 'PATCH'].includes(e.target.value);
            bodySection.style.display = needsBody ? 'block' : 'none';
        });

        document.getElementById('users').addEventListener('change', (e) => {
            document.getElementById('infoUsers').textContent = e.target.value + ' VUs';
        });
    }

    async connectSignalR() {
        this.connection = new signalR.HubConnectionBuilder()
            .withUrl('/hubs/loadtest')
            .withAutomaticReconnect()
            .build();

        this.connection.on('Connected', (data) => {
            console.log('Connected to hub:', data.connectionId);
            this.updateConnectionStatus(true);
        });

        this.connection.on('MetricReceived', (metric) => {
            this.handleMetric(metric);
        });

        this.connection.on('TestCompleted', (result) => {
            console.log('TestCompleted event received:', result);
            this.handleTestCompleted(result);
        });

        this.connection.on('TestError', (error) => {
            console.log('TestError event received:', error);
            this.handleTestError(error);
        });

        this.connection.on('AuthenticationStarted', () => {
            this.showAuthNotification('Authenticating...', 'info');
        });

        this.connection.on('AuthenticationSuccess', () => {
            this.showAuthNotification('Authentication successful!', 'success');
        });

        this.connection.on('AuthenticationFailed', (data) => {
            this.showAuthNotification(`Failed: ${data.error}`, 'error');
        });

        this.connection.onclose(() => {
            this.updateConnectionStatus(false);
        });

        this.connection.onreconnecting(() => {
            this.updateConnectionStatus(false);
        });

        this.connection.onreconnected(async () => {
            this.updateConnectionStatus(true);
            await this.syncTestStatus();
        });

        try {
            await this.connection.start();
            this.updateConnectionStatus(true);
            await this.syncTestStatus();
        } catch (err) {
            console.error('SignalR connection error:', err);
            this.updateConnectionStatus(false);
        }
    }

    async syncTestStatus() {
        try {
            const response = await fetch('/api/test/status');
            const data = await response.json();
            if (!data.isRunning && this.isRunning) {
                console.log('Syncing: test is not running on server, resetting frontend state');
                this.isRunning = false;
                this.toggleButtons(false);
                this.updateTestStatus('idle');
            }
        } catch (err) {
            console.error('Failed to sync test status:', err);
        }
    }

    showAuthNotification(message, type) {
        const authStatus = document.getElementById('authStatus');
        if (authStatus) {
            const colors = { info: '#06b6d4', success: '#10b981', error: '#ef4444' };
            authStatus.innerHTML = `<span style="color: ${colors[type]}; font-size: 0.75rem; margin-left: 8px;">${message}</span>`;
        }
    }

    updateConnectionStatus(connected) {
        const status = document.getElementById('connectionStatus');
        if (connected) {
            status.className = 'connection-badge connected';
            status.innerHTML = '<span class="status-dot"></span><span class="status-text">Connected</span>';
        } else {
            status.className = 'connection-badge disconnected';
            status.innerHTML = '<span class="status-dot"></span><span class="status-text">Disconnected</span>';
        }
    }

    async startTest() {
        const request = this.buildRequest();
        
        if (!request.url) {
            alert('Please enter a valid URL');
            return;
        }

        const saveAsProject = document.getElementById('saveAsProject').checked;
        let targetEndpointId = this.selectedEndpointId;
        
        if (saveAsProject && !targetEndpointId) {
            const existingSection = document.getElementById('existingProjectSection');
            const isExistingProject = !existingSection.classList.contains('hidden');
            
            try {
                if (isExistingProject) {
                    const projectId = document.getElementById('existingProject').value;
                    const endpointName = document.getElementById('endpointName').value;
                    
                    if (!projectId) {
                        alert('Please select a project');
                        return;
                    }
                    if (!endpointName) {
                        alert('Please enter an endpoint name');
                        return;
                    }
                    
                    const endpointDto = this.buildEndpointDto(endpointName, request);
                    const response = await fetch(`/api/projects/${projectId}/endpoints`, {
                        method: 'POST',
                        headers: { 'Content-Type': 'application/json' },
                        body: JSON.stringify(endpointDto)
                    });
                    
                    if (!response.ok) {
                        throw new Error('Failed to create endpoint');
                    }
                    
                    const endpoint = await response.json();
                    targetEndpointId = endpoint.id;
                    
                } else {
                    const projectName = document.getElementById('projectName').value;
                    const projectDescription = document.getElementById('projectDescription').value;
                    const endpointName = document.getElementById('newEndpointName').value;
                    
                    if (!projectName) {
                        alert('Please enter a project name');
                        return;
                    }
                    if (!endpointName) {
                        alert('Please enter an endpoint name');
                        return;
                    }
                    
                    const projectResponse = await fetch('/api/projects', {
                        method: 'POST',
                        headers: { 'Content-Type': 'application/json' },
                        body: JSON.stringify({ name: projectName, description: projectDescription })
                    });
                    
                    if (!projectResponse.ok) {
                        throw new Error('Failed to create project');
                    }
                    
                    const project = await projectResponse.json();
                    
                    const endpointDto = this.buildEndpointDto(endpointName, request);
                    const endpointResponse = await fetch(`/api/projects/${project.id}/endpoints`, {
                        method: 'POST',
                        headers: { 'Content-Type': 'application/json' },
                        body: JSON.stringify(endpointDto)
                    });
                    
                    if (!endpointResponse.ok) {
                        throw new Error('Failed to create endpoint');
                    }
                    
                    const endpoint = await endpointResponse.json();
                    targetEndpointId = endpoint.id;
                }
                
                await this.loadProjects();
                
            } catch (err) {
                console.error('Error saving to project:', err);
                alert('Failed to save to project: ' + err.message);
                return;
            }
        }

        this.resetCharts();
        this.toggleButtons(true);
        this.updateTestStatus('running');
        this.startTime = new Date();
        this.peakRps = 0;

        document.getElementById('infoUsers').textContent = request.users + ' VUs';
        document.getElementById('infoDuration').textContent = request.duration ? request.duration + 's' : request.requests + ' reqs';
        document.getElementById('infoDate').textContent = this.startTime.toLocaleDateString('en-US', {
            day: 'numeric',
            month: 'short',
            year: 'numeric',
            hour: '2-digit',
            minute: '2-digit'
        });

        document.getElementById('configPanel').classList.remove('open');

        try {
            let response;
            
            if (targetEndpointId) {
                response = await fetch(`/api/endpoints/${targetEndpointId}/test/start`, {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify({})
                });
            } else {
                response = await fetch('/api/test/start', {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify(request)
                });
            }

            if (response.ok) {
                const data = await response.json();
                this.testId = data.testId;
                this.isRunning = true;
                await this.loadStatistics();
            } else {
                const error = await response.json();
                throw new Error(error.error || 'Failed to start test');
            }
        } catch (err) {
            console.error('Error starting test:', err);
            alert('Failed to start test: ' + err.message);
            this.toggleButtons(false);
            this.updateTestStatus('idle');
        }
    }
    
    buildEndpointDto(name, request) {
        return {
            name: name,
            url: request.url,
            method: request.method,
            users: request.users,
            requests: request.requests,
            duration: request.duration,
            contentType: request.contentType,
            body: request.body,
            insecure: request.insecure,
            headers: request.headers,
            authentication: request.authentication
        };
    }

    async stopTest() {
        try {
            await fetch('/api/test/stop', { method: 'POST' });
            this.isRunning = false;
        } catch (err) {
            console.error('Error stopping test:', err);
        }
    }

    updateTestStatus(status) {
        const badge = document.getElementById('testStatusBadge');
        const indicator = badge.querySelector('.status-indicator');
        const text = badge.querySelector('span:last-child');
        
        indicator.className = 'status-indicator ' + status;
        
        const labels = { idle: 'Ready', running: 'Running', completed: 'Completed' };
        text.textContent = labels[status] || status;
    }

    buildRequest() {
        const headers = {};
        document.querySelectorAll('.header-row').forEach(row => {
            const key = row.querySelector('.header-key')?.value;
            const value = row.querySelector('.header-value')?.value;
            if (key && value) {
                headers[key] = value;
            }
        });

        const durationValue = document.getElementById('duration').value;
        const useAuth = document.getElementById('useAuth').checked;
        
        const request = {
            url: this.getFullUrl('urlSchema', 'url'),
            method: document.getElementById('method').value,
            users: parseInt(document.getElementById('users').value) || 10,
            requests: durationValue ? null : (parseInt(document.getElementById('requests').value) || 100),
            duration: durationValue ? parseInt(durationValue) : null,
            contentType: document.getElementById('contentType').value,
            body: document.getElementById('body').value || null,
            insecure: document.getElementById('insecure').checked,
            headers: Object.keys(headers).length > 0 ? headers : null
        };

        if (useAuth) {
            request.authentication = this.buildAuthConfig();
        }

        return request;
    }

    buildAuthConfig() {
        return {
            url: this.getFullUrl('authUrlSchema', 'authUrl'),
            method: document.getElementById('authMethod').value,
            body: document.getElementById('authBody').value || null,
            contentType: document.getElementById('authContentType').value,
            tokenPath: document.getElementById('tokenPath').value || 'access_token',
            headerName: document.getElementById('headerName').value || 'Authorization',
            headerPrefix: document.getElementById('headerPrefix').value || 'Bearer '
        };
    }

    formatNumber(num) {
        if (num >= 1000000) return (num / 1000000).toFixed(1) + 'M';
        if (num >= 1000) return (num / 1000).toFixed(1) + 'K';
        return num.toString();
    }

    handleMetric(metric) {
        document.getElementById('statTotalRequests').textContent = this.formatNumber(metric.totalRequests);
        document.getElementById('statFailed').textContent = this.formatNumber(metric.failedRequests);
        document.getElementById('statAvgResponse').textContent = Math.round(metric.averageResponseTime);

        if (metric.currentRps > this.peakRps) {
            this.peakRps = metric.currentRps;
        }
        document.getElementById('statRps').textContent = this.peakRps.toFixed(2);

        document.getElementById('avgResponseBottom').textContent = Math.round(metric.averageResponseTime) + 'ms';
        document.getElementById('totalRequestsBottom').textContent = this.formatNumber(metric.totalRequests);
        document.getElementById('rpsBottom').textContent = metric.currentRps.toFixed(2);

        const now = new Date();
        const timeLabel = now.toLocaleTimeString('en-US', { hour: '2-digit', minute: '2-digit', second: '2-digit' });
        
        this.responseTimeHistory.push({
            time: timeLabel,
            value: metric.responseTime,
            avg: metric.averageResponseTime
        });
        
        if (this.responseTimeHistory.length > this.maxDataPoints) {
            this.responseTimeHistory.shift();
        }

        const rtChart = this.charts.responseTime;
        rtChart.data.labels = this.responseTimeHistory.map(d => d.time);
        rtChart.data.datasets[0].data = this.responseTimeHistory.map(d => d.value);
        rtChart.data.datasets[1].data = this.responseTimeHistory.map(d => d.avg);
        rtChart.update('none');

        const statusGroup = this.getStatusCodeGroup(metric.statusCode);
        this.statusCodeCounts[statusGroup] = (this.statusCodeCounts[statusGroup] || 0) + 1;
        
        const scChart = this.charts.statusCode;
        scChart.data.labels = Object.keys(this.statusCodeCounts);
        scChart.data.datasets[0].data = Object.values(this.statusCodeCounts);
        scChart.update('none');
    }

    getStatusCodeGroup(statusCode) {
        if (statusCode >= 200 && statusCode < 300) return '2xx Success';
        if (statusCode >= 300 && statusCode < 400) return '3xx Redirect';
        if (statusCode >= 400 && statusCode < 500) return '4xx Client Error';
        if (statusCode >= 500) return '5xx Server Error';
        return 'Other';
    }

    handleTestCompleted(result) {
        console.log('handleTestCompleted called, updating UI...');
        this.isRunning = false;
        this.toggleButtons(false);
        this.updateTestStatus('completed');
        console.log('Status updated to completed');

        document.getElementById('statTotalRequests').textContent = this.formatNumber(result.totalRequests);
        document.getElementById('statFailed').textContent = this.formatNumber(result.failedRequests);
        document.getElementById('statRps').textContent = result.requestsPerSecond.toFixed(2);
        document.getElementById('statAvgResponse').textContent = Math.round(result.averageResponseTime);
        
        document.getElementById('avgResponseBottom').textContent = Math.round(result.averageResponseTime) + 'ms';
        document.getElementById('totalRequestsBottom').textContent = this.formatNumber(result.totalRequests);
        document.getElementById('rpsBottom').textContent = result.requestsPerSecond.toFixed(2);

        const pctChart = this.charts.percentile;
        pctChart.data.datasets[0].data = [
            result.percentile50,
            result.percentile75,
            result.percentile90,
            result.percentile95,
            result.percentile99
        ];
        pctChart.update();

        this.displayFinalResults(result);
    }

    displayFinalResults(result) {
        const resultsCard = document.getElementById('resultsCard');
        resultsCard.classList.remove('hidden');

        const globalBody = document.getElementById('globalResultsBody');
        globalBody.innerHTML = `
            <tr><td>Total Duration</td><td><strong>${(result.totalElapsedTime / 1000).toFixed(2)}s</strong></td></tr>
            <tr><td>Total Requests</td><td><strong>${result.totalRequests.toLocaleString()}</strong></td></tr>
            <tr><td>Successful</td><td><strong style="color: #10b981">${result.successfulRequests.toLocaleString()}</strong></td></tr>
            <tr><td>Failed</td><td><strong style="color: #ef4444">${result.failedRequests.toLocaleString()}</strong></td></tr>
            <tr><td>Requests/sec</td><td><strong>${result.requestsPerSecond.toFixed(2)}</strong></td></tr>
            <tr><td>Avg Response Time</td><td><strong>${result.averageResponseTime.toFixed(2)}ms</strong></td></tr>
            <tr><td>Min Response Time</td><td><strong>${result.minResponseTime.toFixed(2)}ms</strong></td></tr>
            <tr><td>Max Response Time</td><td><strong>${result.maxResponseTime.toFixed(2)}ms</strong></td></tr>
        `;

        const pctBody = document.getElementById('percentileResultsBody');
        pctBody.innerHTML = `
            <tr><td>P50</td><td><strong>${result.percentile50.toFixed(2)}ms</strong></td></tr>
            <tr><td>P75</td><td><strong>${result.percentile75.toFixed(2)}ms</strong></td></tr>
            <tr><td>P90</td><td><strong>${result.percentile90.toFixed(2)}ms</strong></td></tr>
            <tr><td>P95</td><td><strong>${result.percentile95.toFixed(2)}ms</strong></td></tr>
            <tr><td>P99</td><td><strong>${result.percentile99.toFixed(2)}ms</strong></td></tr>
        `;

        const scBody = document.getElementById('statusCodeResultsBody');
        scBody.innerHTML = '';
        for (const [code, data] of Object.entries(result.statusCodes)) {
            const badgeColor = code.startsWith('2') ? '#10b981' : code.startsWith('4') ? '#ef4444' : code.startsWith('5') ? '#8b5cf6' : '#6b7280';
            scBody.innerHTML += `
                <tr>
                    <td><span style="background: ${badgeColor}; color: white; padding: 2px 8px; border-radius: 4px; font-size: 0.75rem;">${code}</span></td>
                    <td>${data.count}</td>
                    <td>${data.minResponseTime.toFixed(1)}ms</td>
                    <td>${data.avgResponseTime.toFixed(1)}ms</td>
                    <td>${data.maxResponseTime.toFixed(1)}ms</td>
                    <td>${data.percentile50.toFixed(1)}ms</td>
                    <td>${data.percentile90.toFixed(1)}ms</td>
                    <td>${data.percentile99.toFixed(1)}ms</td>
                </tr>
            `;
        }

        resultsCard.scrollIntoView({ behavior: 'smooth' });
    }

    handleTestError(error) {
        this.isRunning = false;
        this.toggleButtons(false);
        this.updateTestStatus('idle');
        alert('Test error: ' + error.error);
    }

    resetCharts() {
        this.statusCodeCounts = {};
        this.responseTimeHistory = [];
        this.rpsHistory = [];
        this.peakRps = 0;

        document.getElementById('statTotalRequests').textContent = '0';
        document.getElementById('statFailed').textContent = '0';
        document.getElementById('statRps').textContent = '0';
        document.getElementById('statAvgResponse').textContent = '0';
        document.getElementById('avgResponseBottom').textContent = '0ms';
        document.getElementById('totalRequestsBottom').textContent = '0';
        document.getElementById('rpsBottom').textContent = '0';

        document.getElementById('resultsCard').classList.add('hidden');

        this.charts.responseTime.data.labels = [];
        this.charts.responseTime.data.datasets[0].data = [];
        this.charts.responseTime.data.datasets[1].data = [];
        this.charts.responseTime.update();

        this.charts.statusCode.data.labels = [];
        this.charts.statusCode.data.datasets[0].data = [];
        this.charts.statusCode.update();

        this.charts.percentile.data.datasets[0].data = [0, 0, 0, 0, 0];
        this.charts.percentile.update();
    }

    toggleButtons(isRunning) {
        const startBtn = document.getElementById('startBtn');
        const stopBtn = document.getElementById('stopBtn');
        const form = document.getElementById('testForm');

        if (isRunning) {
            startBtn.classList.add('hidden');
            stopBtn.classList.remove('hidden');
            form.querySelectorAll('input, select, textarea').forEach(el => { el.disabled = true; });
        } else {
            startBtn.classList.remove('hidden');
            stopBtn.classList.add('hidden');
            form.querySelectorAll('input, select, textarea').forEach(el => { el.disabled = false; });
        }
    }
}

function initSidebarResize() {
    const sidebar = document.getElementById('sidebar');
    const handle = document.getElementById('sidebarResizeHandle');
    let isResizing = false;
    let startX = 0;
    let startWidth = 0;

    handle.addEventListener('mousedown', (e) => {
        isResizing = true;
        startX = e.clientX;
        startWidth = sidebar.offsetWidth;
        sidebar.classList.add('resizing');
        handle.classList.add('active');
        document.body.style.cursor = 'col-resize';
        document.body.style.userSelect = 'none';
        e.preventDefault();
    });

    document.addEventListener('mousemove', (e) => {
        if (!isResizing) return;
        
        const diff = e.clientX - startX;
        let newWidth = startWidth + diff;
        
        newWidth = Math.max(180, Math.min(400, newWidth));
        
        sidebar.style.width = newWidth + 'px';
        
        document.documentElement.style.setProperty('--sidebar-width', newWidth + 'px');
        
        const mainContent = document.querySelector('.main-content.with-sidebar');
        const testInfoBar = document.querySelector('.test-info-bar');
        
        if (document.body.classList.contains('sidebar-open')) {
            if (mainContent) mainContent.style.marginLeft = newWidth + 'px';
            if (testInfoBar) testInfoBar.style.left = newWidth + 'px';
        }
    });

    document.addEventListener('mouseup', () => {
        if (!isResizing) return;
        
        isResizing = false;
        sidebar.classList.remove('resizing');
        handle.classList.remove('active');
        document.body.style.cursor = '';
        document.body.style.userSelect = '';
        
        localStorage.setItem('nfury-sidebar-width', sidebar.offsetWidth);
    });

    const savedWidth = localStorage.getItem('nfury-sidebar-width');
    if (savedWidth) {
        const width = parseInt(savedWidth);
        if (width >= 180 && width <= 400) {
            sidebar.style.width = width + 'px';
            if (document.body.classList.contains('sidebar-open')) {
                const mainContent = document.querySelector('.main-content.with-sidebar');
                const testInfoBar = document.querySelector('.test-info-bar');
                if (mainContent) mainContent.style.marginLeft = width + 'px';
                if (testInfoBar) testInfoBar.style.left = width + 'px';
            }
        }
    }
}

document.addEventListener('DOMContentLoaded', initSidebarResize);

function toggleConfigPanel() {
    const panel = document.getElementById('configPanel');
    panel.classList.toggle('open');
}

function toggleSidebar() {
    const sidebar = document.getElementById('sidebar');
    sidebar.classList.toggle('open');
    document.body.classList.toggle('sidebar-open');
}

function toggleHistoryPanel() {
    const panel = document.getElementById('historyPanel');
    const isOpen = panel.classList.toggle('open');
    if (isOpen) {
        window.app.loadHistory();
    }
    document.getElementById('overlay').classList.toggle('visible', isOpen);
}

function loadHistory() {
    const status = document.getElementById('historyStatusFilter').value;
    window.app.loadHistory(status);
}

function toggleTheme() {
    const html = document.documentElement;
    const themeIcon = document.getElementById('themeIcon');
    const currentTheme = html.getAttribute('data-theme');
    
    if (currentTheme === 'dark') {
        html.removeAttribute('data-theme');
        themeIcon.classList.remove('fa-sun');
        themeIcon.classList.add('fa-moon');
        localStorage.setItem('theme', 'light');
    } else {
        html.setAttribute('data-theme', 'dark');
        themeIcon.classList.remove('fa-moon');
        themeIcon.classList.add('fa-sun');
        localStorage.setItem('theme', 'dark');
    }
}

function initializeTheme() {
    const savedTheme = localStorage.getItem('theme');
    const themeIcon = document.getElementById('themeIcon');
    
    if (savedTheme === 'dark') {
        document.documentElement.setAttribute('data-theme', 'dark');
        themeIcon.classList.remove('fa-moon');
        themeIcon.classList.add('fa-sun');
    }
}

function selectQuickTest() {
    window.app.selectedProjectId = null;
    window.app.selectedEndpointId = null;
    window.app.renderProjectList();
    document.getElementById('projectNameHeader').textContent = 'Quick Test';
    
    document.getElementById('urlSchema').value = 'https://';
    document.getElementById('url').value = '';
    document.getElementById('method').value = 'GET';
    document.getElementById('users').value = 10;
    document.getElementById('requests').value = 100;
    document.getElementById('duration').value = '';
    document.getElementById('body').value = '';
    document.getElementById('headersContainer').innerHTML = '';
    document.getElementById('useAuth').checked = false;
    toggleAuthSection();
}

function showCreateProjectModal() {
    document.getElementById('newProjectName').value = '';
    document.getElementById('newProjectDescription').value = '';
    document.getElementById('createProjectModal').classList.add('open');
    document.getElementById('overlay').classList.add('visible');
}

function closeCreateProjectModal() {
    document.getElementById('createProjectModal').classList.remove('open');
    document.getElementById('overlay').classList.remove('visible');
}

function closeEndpointModal() {
    document.getElementById('endpointModal').classList.remove('open');
    document.getElementById('overlay').classList.remove('visible');
}

function closeExecutionModal() {
    document.getElementById('executionModal').classList.remove('open');
    document.getElementById('overlay').classList.remove('visible');
}

function closeAllModals() {
    document.getElementById('createProjectModal').classList.remove('open');
    document.getElementById('endpointModal').classList.remove('open');
    document.getElementById('executionModal').classList.remove('open');
    document.getElementById('projectAuthModal').classList.remove('open');
    document.getElementById('importProjectModal').classList.remove('open');
    document.getElementById('historyPanel').classList.remove('open');
    document.getElementById('overlay').classList.remove('visible');
    
    const fileInput = document.getElementById('importProjectFile');
    if (fileInput) fileInput.value = '';
    document.getElementById('importPreview').style.display = 'none';
    document.getElementById('importError').style.display = 'none';
    document.getElementById('btnImportProject').disabled = true;
    window.pendingImportData = null;
}

function showImportProjectModal() {
    document.getElementById('importProjectModal').classList.add('open');
    document.getElementById('overlay').classList.add('visible');
}

function handleImportFileSelect(event) {
    const file = event.target.files[0];
    if (!file) return;
    
    const reader = new FileReader();
    reader.onload = function(e) {
        try {
            const data = JSON.parse(e.target.result);
            
            if (!data.project || !data.project.name) {
                showImportError('Invalid file format: missing project data');
                return;
            }
            
            document.getElementById('importProjectName').textContent = data.project.name;
            document.getElementById('importEndpointsCount').textContent = data.project.endpoints?.length || 0;
            
            let totalExecutions = 0;
            if (data.project.endpoints) {
                data.project.endpoints.forEach(ep => {
                    totalExecutions += ep.executions?.length || 0;
                });
            }
            document.getElementById('importExecutionsCount').textContent = totalExecutions;
            document.getElementById('importHasAuth').textContent = data.project.authUrl ? 'Yes' : 'No';
            
            document.getElementById('importPreview').style.display = 'block';
            document.getElementById('importError').style.display = 'none';
            document.getElementById('btnImportProject').disabled = false;
            
            window.pendingImportData = data;
        } catch (err) {
            showImportError('Invalid JSON file: ' + err.message);
        }
    };
    reader.readAsText(file);
}

function showImportError(message) {
    document.getElementById('importError').textContent = message;
    document.getElementById('importError').style.display = 'block';
    document.getElementById('importPreview').style.display = 'none';
    document.getElementById('btnImportProject').disabled = true;
}

async function importProject() {
    if (!window.pendingImportData) return;
    
    const btn = document.getElementById('btnImportProject');
    const originalText = btn.innerHTML;
    btn.innerHTML = '<i class="fas fa-spinner fa-spin"></i> Importing...';
    btn.disabled = true;
    
    try {
        const response = await fetch('/api/projects/import', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(window.pendingImportData)
        });
        
        if (!response.ok) {
            const error = await response.json();
            throw new Error(error.error || 'Import failed');
        }
        
        const result = await response.json();
        closeAllModals();
        
        if (result.projectId) {
            window.app.expandedProjects.add(result.projectId);
        }
        await window.app.loadProjects();
        
        alert(`Project imported successfully!\n\n• Project: ${result.projectName}\n• Endpoints: ${result.endpointsImported}\n• Executions: ${result.executionsImported}`);
    } catch (err) {
        showImportError(err.message);
        btn.innerHTML = originalText;
        btn.disabled = false;
    }
}

async function exportProject(projectId, projectName) {
    try {
        const response = await fetch(`/api/projects/${projectId}/export`);
        if (!response.ok) {
            throw new Error('Export failed');
        }
        
        const data = await response.json();
        
        const blob = new Blob([JSON.stringify(data, null, 2)], { type: 'application/json' });
        const url = URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = `${projectName.replace(/[^a-z0-9]/gi, '_').toLowerCase()}_export.json`;
        document.body.appendChild(a);
        a.click();
        document.body.removeChild(a);
        URL.revokeObjectURL(url);
    } catch (err) {
        alert('Export failed: ' + err.message);
    }
}

async function createProject() {
    const name = document.getElementById('newProjectName').value;
    const description = document.getElementById('newProjectDescription').value;
    
    if (!name) {
        alert('Please enter a project name');
        return;
    }
    
    const project = await window.app.createProject({
        name,
        description
    });
    
    if (project) {
        closeCreateProjectModal();
        window.app.expandedProjects.add(project.id);
        await window.app.loadProjects();
    }
}

async function saveEndpoint() {
    await window.app.saveEndpoint();
}

function toggleProjectSaveOptions() {
    const options = document.getElementById('projectSaveOptions');
    const checked = document.getElementById('saveAsProject').checked;
    options.classList.toggle('hidden', !checked);
    
    if (checked) {
        populateProjectsDropdown();
    }
}

async function populateProjectsDropdown() {
    const select = document.getElementById('existingProject');
    const projects = window.app.projects || [];
    
    select.innerHTML = '<option value="">-- Select a project --</option>';
    
    for (const project of projects) {
        const option = document.createElement('option');
        option.value = project.id;
        option.textContent = project.name;
        select.appendChild(option);
    }
}

function selectProjectTab(tab) {
    const tabs = document.querySelectorAll('.project-tab');
    const existingSection = document.getElementById('existingProjectSection');
    const newSection = document.getElementById('newProjectSection');
    
    tabs.forEach(t => t.classList.remove('active'));
    
    if (tab === 'existing') {
        tabs[0].classList.add('active');
        existingSection.classList.remove('hidden');
        newSection.classList.add('hidden');
    } else {
        tabs[1].classList.add('active');
        existingSection.classList.add('hidden');
        newSection.classList.remove('hidden');
    }
}

function toggleProjectNameInput() {
    toggleProjectSaveOptions();
}

function addHeader() {
    const container = document.getElementById('headersContainer');
    const row = document.createElement('div');
    row.className = 'header-row';
    row.innerHTML = `
        <input type="text" class="header-key" placeholder="Header name">
        <input type="text" class="header-value" placeholder="Header value">
        <button type="button" class="btn-remove-header" onclick="this.parentElement.remove()">
            <i class="fas fa-times"></i>
        </button>
    `;
    container.appendChild(row);
}

function addEndpointHeader(key = '', value = '') {
    const container = document.getElementById('endpointHeadersContainer');
    const row = document.createElement('div');
    row.className = 'header-row';
    row.innerHTML = `
        <input type="text" class="header-key" placeholder="Header name" value="${key}">
        <input type="text" class="header-value" placeholder="Header value" value="${value}">
        <button type="button" class="btn-remove-header" onclick="this.parentElement.remove()">
            <i class="fas fa-times"></i>
        </button>
    `;
    container.appendChild(row);
}

function addProjectAuthHeader(key = '', value = '') {
    const container = document.getElementById('projectAuthHeadersContainer');
    const row = document.createElement('div');
    row.className = 'header-row';
    row.innerHTML = `
        <input type="text" class="header-key" placeholder="Header name" value="${key}">
        <input type="text" class="header-value" placeholder="Header value" value="${value}">
        <button type="button" class="btn-remove-header" onclick="this.parentElement.remove()">
            <i class="fas fa-times"></i>
        </button>
    `;
    container.appendChild(row);
}

async function saveProjectAuth() {
    await window.app.saveProjectAuth();
}

async function deleteProjectAuth() {
    await window.app.deleteProjectAuth();
}

function toggleAuthSection() {
    const authSection = document.getElementById('authSection');
    const useAuth = document.getElementById('useAuth').checked;
    
    if (useAuth) {
        authSection.classList.remove('hidden');
    } else {
        authSection.classList.add('hidden');
    }
}

async function testAuthentication() {
    const authStatus = document.getElementById('authStatus');
    
    const authConfig = window.app.buildAuthConfig();
    const insecure = document.getElementById('insecure').checked;

    if (!authConfig.url) {
        authStatus.innerHTML = '<div class="auth-error"><i class="fas fa-exclamation-circle"></i> Please enter an auth URL</div>';
        return;
    }

    authStatus.innerHTML = '<div class="auth-loading"><i class="fas fa-spinner fa-spin"></i> Testing...</div>';

    try {
        const response = await fetch('/api/auth/test', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ config: authConfig, insecure })
        });

        const result = await response.json();

        if (result.success) {
            authStatus.innerHTML = '<div class="auth-success"><i class="fas fa-check-circle"></i> Authentication successful!</div>';
        } else {
            authStatus.innerHTML = `<div class="auth-error"><i class="fas fa-times-circle"></i> ${result.error}</div>`;
        }
    } catch (err) {
        authStatus.innerHTML = `<div class="auth-error"><i class="fas fa-times-circle"></i> ${err.message}</div>`;
    }
}

document.addEventListener('DOMContentLoaded', () => {
    initializeTheme();
    window.app = new NFuryApp();
});
