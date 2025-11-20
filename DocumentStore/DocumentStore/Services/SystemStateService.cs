using System.Threading.Tasks;
using Grpc.Core;
using DocumentStore.Protos;
using Taxxor.Project;
using System.Xml;
using static Framework;
using static Taxxor.Project.ProjectLogic;

namespace DocumentStore.Services
{
    public class SystemStateService : Protos.SystemStateService.SystemStateServiceBase
    {
        public override async Task<TaxxorGrpcResponseMessage> GetSystemState(
            GetSystemStateRequest request, ServerCallContext context)
        {
            try
            {
                // Get the current context variables
                var reqVars = ProjectLogic.RetrieveRequestVariables(System.Web.Context.Current);
                
                // Call the core logic directly
                var result = await ProjectLogic.RetrieveSystemStateCore(reqVars);
                
                if (result.Success)
                {
                    var xmlContent = "";
                    if (result.ResponseContent is XmlDocument)
                    {
                        xmlContent = ((XmlDocument)result.ResponseContent).OuterXml;
                    }
                    else if (result.ResponseContent is string)
                    {
                        xmlContent = (string)result.ResponseContent;
                    }
                    else if (result.ResponseContent is TaxxorReturnMessage)
                    {
                        var trm = (TaxxorReturnMessage)result.ResponseContent;
                        xmlContent = trm.XmlPayload?.OuterXml ?? trm.Payload;
                    }
                    
                    return new TaxxorGrpcResponseMessage
                    {
                        Data = xmlContent,
                        Success = true,
                        Message = "System state retrieved successfully"
                    };
                }
                else
                {
                    return new TaxxorGrpcResponseMessage
                    {
                        Data = string.Empty,
                        Success = false,
                        Message = result.ErrorMessage
                    };
                }
            }
            catch (System.Exception ex)
            {
                return new TaxxorGrpcResponseMessage
                {
                    Data = string.Empty,
                    Success = false,
                    Message = $"Error retrieving system state: {ex.Message}",
                    Debuginfo = $"Exception: {ex.ToString()}"
                };
            }
        }
        
        public override async Task<TaxxorGrpcResponseMessage> SetSystemState(
            SetSystemStateRequest request, ServerCallContext context)
        {
            try
            {
                // Get the current context variables
                var reqVars = ProjectLogic.RetrieveRequestVariables(System.Web.Context.Current);
                
                // Call the core logic directly
                var result = await ProjectLogic.SetSystemStateCore(request.State, reqVars);
                
                if (result.Success)
                {
                    var responseContent = "";
                    if (result.ResponseContent is TaxxorReturnMessage)
                    {
                        responseContent = ((TaxxorReturnMessage)result.ResponseContent).ToString();
                    }
                    else if (result.ResponseContent is string)
                    {
                        responseContent = (string)result.ResponseContent;
                    }
                    
                    return new TaxxorGrpcResponseMessage
                    {
                        Data = responseContent,
                        Success = true,
                        Message = "System state updated successfully"
                    };
                }
                else
                {
                    return new TaxxorGrpcResponseMessage
                    {
                        Data = string.Empty,
                        Success = false,
                        Message = result.ErrorMessage
                    };
                }
            }
            catch (System.Exception ex)
            {
                return new TaxxorGrpcResponseMessage
                {
                    Data = string.Empty,
                    Success = false,
                    Message = $"Error updating system state: {ex.Message}",
                    Debuginfo = $"Exception: {ex.ToString()}"
                };
            }
        }
    }
}