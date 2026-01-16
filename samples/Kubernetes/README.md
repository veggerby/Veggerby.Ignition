# Kubernetes Sample

This sample demonstrates how to deploy a Veggerby.Ignition application to Kubernetes with proper health check integration. It showcases the recommended patterns for liveness, readiness, and startup probes in containerized environments.

## Overview

The sample includes:
- ASP.NET Core WebAPI with Ignition signals for database and cache connections
- Docker containerization with multi-stage build
- Kubernetes deployment manifest with all three probe types
- Service definition for cluster access
- Docker Compose for local testing

## Key Concepts

### Health Check Endpoints

The application exposes three health check endpoints aligned with Kubernetes probe requirements:

1. **Liveness Probe** (`/health/live`)
   - Checks if the application process is alive
   - Returns healthy as long as the app is running
   - Does NOT include Ignition status (prevents restart loops during long initialization)
   - Kubernetes restarts the pod if this fails

2. **Readiness Probe** (`/health/ready`)
   - Checks if the application is ready to serve traffic
   - Includes Ignition health check status
   - Kubernetes routes traffic only when this succeeds
   - Allows the pod to temporarily become unready without restarting

3. **Startup Probe** (`/health/startup`)
   - Checks if the application has completed initialization
   - Includes Ignition health check status
   - Gives the app up to 50 seconds to start (10 failures × 5 seconds)
   - Liveness and readiness probes don't run until startup succeeds

### Ignition Signals

The sample includes three signals simulating common startup dependencies:

- **PostgresConnectionSignal**: Database connection initialization (10s timeout)
- **RedisConnectionSignal**: Cache connection initialization (8s timeout)
- **KubernetesConfigSignal**: ConfigMap/Secret loading (5s timeout)

All signals run in parallel with a 30-second global timeout using `BestEffort` policy.

## Prerequisites

- .NET 10.0 SDK
- Docker
- Kubernetes cluster (minikube, kind, or cloud provider)
- kubectl CLI

## Running Locally with Docker Compose

Build and run using Docker Compose:

```bash
cd /path/to/Veggerby.Ignition
docker-compose -f samples/Kubernetes/docker-compose.yml up --build
```

Test the endpoints:

```bash
# Check liveness
curl http://localhost:8080/health/live

# Check readiness (includes ignition status)
curl http://localhost:8080/health/ready

# Check startup status
curl http://localhost:8080/health/startup

# Get detailed status
curl http://localhost:8080/api/status
```

Stop the container:

```bash
docker-compose -f samples/Kubernetes/docker-compose.yml down
```

## Deploying to Kubernetes

### 1. Build and Push Docker Image

```bash
cd /path/to/Veggerby.Ignition

# Build the image
docker build -t veggerby/ignition-k8s-sample:latest -f samples/Kubernetes/Dockerfile .

# Push to registry (replace with your registry)
docker tag veggerby/ignition-k8s-sample:latest your-registry/ignition-k8s-sample:latest
docker push your-registry/ignition-k8s-sample:latest
```

### 2. Update Deployment Manifest

Edit `deployment.yaml` to use your image:

```yaml
spec:
  template:
    spec:
      containers:
      - name: api
        image: your-registry/ignition-k8s-sample:latest
```

### 3. Deploy to Kubernetes

```bash
# Create deployment
kubectl apply -f samples/Kubernetes/deployment.yaml

# Create service
kubectl apply -f samples/Kubernetes/service.yaml

# Check deployment status
kubectl get deployments
kubectl get pods

# Check pod logs
kubectl logs -l app=ignition-k8s-sample --tail=50 -f
```

### 4. Test the Deployment

```bash
# Port-forward to test locally
kubectl port-forward svc/ignition-k8s-sample 8080:80

# In another terminal, test endpoints
curl http://localhost:8080/health/live
curl http://localhost:8080/health/ready
curl http://localhost:8080/api/status
```

## Probe Configuration Explanation

### Startup Probe
```yaml
startupProbe:
  httpGet:
    path: /health/startup
    port: http
  initialDelaySeconds: 0        # Start checking immediately
  periodSeconds: 5              # Check every 5 seconds
  timeoutSeconds: 3             # Request timeout
  failureThreshold: 10          # Allow 10 failures (50s total)
```

- Gives the application 50 seconds to complete initialization
- Suitable for Ignition's 30-second global timeout + overhead
- Prevents liveness probe from killing the pod during startup

### Readiness Probe
```yaml
readinessProbe:
  httpGet:
    path: /health/ready
    port: http
  initialDelaySeconds: 5        # Wait 5s after startup succeeds
  periodSeconds: 5              # Check every 5 seconds
  failureThreshold: 3           # Allow 3 failures before marking unready
```

- Only starts after startup probe succeeds
- Temporarily removes pod from service if initialization regresses
- Doesn't restart the pod, just stops routing traffic

### Liveness Probe
```yaml
livenessProbe:
  httpGet:
    path: /health/live
    port: http
  initialDelaySeconds: 10       # Wait 10s after startup succeeds
  periodSeconds: 10             # Check every 10 seconds
  failureThreshold: 3           # Allow 3 failures before restarting
```

- Only starts after startup probe succeeds
- Does NOT include Ignition checks (prevents restart during initialization)
- Only restarts if the process is truly dead

## Best Practices Demonstrated

1. **Non-root Container**: Runs as user 1000 for security
2. **Resource Limits**: CPU and memory constraints defined
3. **Security Context**: Read-only root filesystem, dropped capabilities
4. **Multi-stage Build**: Minimal runtime image size
5. **Probe Separation**: Different endpoints for different probe types
6. **Graceful Startup**: Startup probe protects during initialization
7. **Health Check Tags**: Ignition health check tagged with "ready" and "startup"
8. **Parallel Initialization**: Signals run concurrently with controlled parallelism

## Monitoring Startup

Watch the pod startup process:

```bash
# Watch pod events
kubectl get events --watch

# Follow pod logs
kubectl logs -l app=ignition-k8s-sample -f

# Check probe status
kubectl describe pod -l app=ignition-k8s-sample | grep -A 10 "Conditions:"
```

## Troubleshooting

### Pod Stuck in CrashLoopBackOff

- Check if startup probe `failureThreshold` is sufficient for your initialization time
- Review pod logs: `kubectl logs -l app=ignition-k8s-sample --previous`
- Increase `failureThreshold` or `periodSeconds` in startup probe if needed

### Pod Not Ready

- Check readiness probe status: `kubectl describe pod -l app=ignition-k8s-sample`
- Test readiness endpoint: `kubectl port-forward` and `curl /health/ready`
- Review Ignition signal failures in pod logs

### Signals Timing Out

- Adjust individual signal timeouts or global timeout in `Program.cs`
- Increase startup probe duration if needed
- Check external dependencies (database, cache) are accessible from pod

## Cleanup

```bash
# Delete all resources
kubectl delete -f samples/Kubernetes/deployment.yaml
kubectl delete -f samples/Kubernetes/service.yaml

# Verify deletion
kubectl get all -l app=ignition-k8s-sample
```

## Additional Resources

- [Kubernetes Probes Documentation](https://kubernetes.io/docs/tasks/configure-pod-container/configure-liveness-readiness-startup-probes/)
- [ASP.NET Core Health Checks](https://learn.microsoft.com/aspnet/core/host-and-deploy/health-checks)
- [Veggerby.Ignition Health Check Integration](../../README.md#health-check-integration)
