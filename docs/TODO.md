# RBAC 有问题

```
frontend/src/router/index.js
requiresAdmin: false 
```

```
frontend/src/App.vue
<router-link v-if="true" to="/system-status">系统状态</router-link>
```