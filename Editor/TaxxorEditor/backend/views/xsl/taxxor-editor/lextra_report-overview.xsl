<?xml version="1.0" encoding="utf-8"?>
<xsl:stylesheet version="1.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform">

	<xsl:param name="url"/>
	<xsl:param name="guidLegalEntity"/>
	<xsl:param name="guidCalendarEvent">
		<xsl:text>none</xsl:text>
	</xsl:param>

	<xsl:output encoding="UTF-8" indent="yes" method="xml" omit-xml-declaration="yes"/>

	<xsl:template match="/">
		<xsl:choose>
			
			<!-- create new filing from overview mode -->
			<xsl:when test="$guidCalendarEvent='none'">
				
				<xsl:choose>
					<xsl:when test="count(data/reporting_requirements[@guidLegalEntity=$guidLegalEntity]/reporting_requirement)=0">
						<div class="alert alert-danger">No filings found..</div>
					</xsl:when>
					<xsl:otherwise>
						<h5>You can create the following filings</h5>
						<xsl:apply-templates select="data/reporting_requirements[@guidLegalEntity=$guidLegalEntity]/reporting_requirement"/>
					</xsl:otherwise>
				</xsl:choose>

			</xsl:when>
			
			<!-- create filing from calendar view mode -->
			<xsl:otherwise>
				
				<div class="modal fade">
					<div class="modal-dialog calendar-event">
						<div class="modal-content">
							<div class="modal-body">
								<button style="margin-top:-10px;" data-dismiss="modal" class="close" type="button">&#215;</button>

								<!-- modal content -->
								<div style="padding-top: 20px;" class="panel">
									<xsl:apply-templates select="data/reporting_requirements[@guidLegalEntity=$guidLegalEntity]/reporting_requirement" mode="calendar_event"/>
								</div>
							</div>
						</div>
					</div>
				</div>
			</xsl:otherwise>

		</xsl:choose>



	</xsl:template>

	<xsl:template match="reporting_requirement">
		<div class="panel panel-info" id="panel_{@guidLegalEntityRequirement}">
			<div class="panel-heading">
				<xsl:value-of select="name"/>
			</div>
			<div class="panel-body">
				<p>Choose a report/filing from the list below</p>
			</div>

			<!-- List group -->
			<div class="list-group">
				<xsl:apply-templates select="requirement/report">
					<xsl:with-param name="guidLegalEntityRequirement" select="@guidLegalEntityRequirement"/>
					<xsl:with-param name="uri_entrypoint" select="requirement/entrypoint/location"/>
					<xsl:with-param name="ready" select="@ready"/>
					<xsl:with-param name="schedule" select="requirement/nextFilings/requirementSchedule[@status='free' and @passedDate='no']"/>
				</xsl:apply-templates>
			</div>
		</div>
	</xsl:template>

	<xsl:template match="reporting_requirement" mode="calendar_event">

		<xsl:if test="requirement[nextFilings/requirementSchedule/identifier=$guidCalendarEvent]/report">

			<div class="panel panel-info" id="panel_{@guidLegalEntityRequirement}">
				<!-- List group -->
				<div class="list-group">
					<xsl:apply-templates select="requirement[nextFilings/requirementSchedule/identifier=$guidCalendarEvent]/report">
						<xsl:with-param name="guidLegalEntityRequirement" select="@guidLegalEntityRequirement"/>
						<xsl:with-param name="uri_entrypoint" select="requirement/entrypoint/location"/>
						<xsl:with-param name="ready" select="@ready"/>
						<xsl:with-param name="schedule" select="requirement/nextFilings/requirementSchedule[@status='free' and @passedDate='no']"/>
						<xsl:with-param name="status" select="requirement/nextFilings/requirementSchedule[identifier=$guidCalendarEvent]/@status"/>
						<xsl:with-param name="mode">calendar_event</xsl:with-param>
					</xsl:apply-templates>
				</div>
			</div>
		</xsl:if>


	</xsl:template>


	<xsl:template match="report">
		<xsl:param name="guidLegalEntityRequirement"/>
		<xsl:param name="uri_entrypoint"/>
		<xsl:param name="ready"/>
		<xsl:param name="schedule"/>
		<xsl:param name="mode">
			<xsl:text>none</xsl:text>
		</xsl:param>
		<xsl:param name="status"/>

		<div class="list-group-item" id="report_{guid}">
			<h5>
				<xsl:value-of select="name"/>
			</h5>

			<dl class="dl-horizontal">
				<dt>Online information</dt>
				<dd>
					<a href="{web}" target="_blank">
						<xsl:value-of select="web"/>
					</a>
				</dd>
				<dt>Languages</dt>
				<dd>
					<xsl:value-of select="languages"/>
				</dd>
				<dt>Currency</dt>
				<dd>
					<xsl:value-of select="currency"/>
				</dd>
			</dl>

			<xsl:choose>
				<xsl:when test="$ready='true'">
					<xsl:choose>
						<xsl:when test="$mode='calendar_event'">
							<!-- only show the button when there is no filing created yet for this calendar event -->
							<xsl:choose>
								<xsl:when test="$status='free'">
									<button type="button" class="btn btn-sm btn-primary select_button" onclick="setupProjectShowFormCalendarEvent('{$guidLegalEntityRequirement}', '{guid}');">Create filing</button>	
								</xsl:when>
								<xsl:otherwise>
									<a href="{$url}" class="btn btn-primary btn-xs"><span class="ace-icon fa fa-share"><xsl:text> </xsl:text></span> Open</a>
								</xsl:otherwise>
							</xsl:choose>

						</xsl:when>
						<xsl:otherwise>
							<button type="button" class="btn btn-sm btn-primary select_button" data-reqid="{$guidLegalEntityRequirement}" data-reportid="{guid}">Select</button>
						</xsl:otherwise>
					</xsl:choose>
				</xsl:when>
				<xsl:otherwise>
					<div class="label label-danger">Editor and configuration not available...</div>
					<div class="space-6"/>
					<button class="btn btn-info btn-xs" onclick="openRequestEditorModal('{$uri_entrypoint}')">
						<i class="glyphicon glyphicon-envelope"/> Request an editor </button>
				</xsl:otherwise>
			</xsl:choose>



			<div class="create_form" style="display: none">
				<form class="form-horizontal" role="form">
					<!--
					<div class="form-group">
						<label for="inputProjectId" class="col-lg-4 control-label">Project id</label>
						<div class="col-lg-6">
							<input type="text" class="form-control inputProjectId" name="inputProjectId" placeholder="Project ID" required="true" data-validation-required-message="You must supply a project id"/>
						</div>
					</div>
					-->
					<div class="form-group">
						<label for="inputProjectName" class="col-lg-4 control-label">Project name</label>
						<div class="col-lg-6">
							<input type="text" class="form-control inputProjectName" name="inputProjectName" placeholder="Project or filing name"/>
						</div>
					</div>

					<!-- filing dates -->
					<xsl:choose>
						<xsl:when test="$mode='calendar_event'">
							<input type="hidden" class="inputProjectFilingDeadline" name="inputProjectFilingDeadline" value="{$guidCalendarEvent}"/>
						</xsl:when>
						<xsl:otherwise>
							<div class="form-group">
								<label for="inputProjectFilingDeadline" class="col-lg-4 control-label">Filing date</label>
								<div class="col-lg-6">
									<xsl:choose>
										<xsl:when test="count($schedule)=0">
											<xsl:text>There are no open filing schedule dates available anymore.</xsl:text>
											<input type="hidden" class="inputProjectFilingDeadline" name="inputProjectFilingDeadline" value="none"/>
										</xsl:when>
										<xsl:otherwise>
											<select class="form-control inputProjectFilingDeadline" name="inputProjectFilingDeadline">
												<option value="" default="default">-- Select a filing date --</option>
												<xsl:for-each select="$schedule">
													<option value="{identifier}">
														<xsl:value-of select="filingDate/@isoDate"/>
													</option>
												</xsl:for-each>
											</select>
										</xsl:otherwise>
									</xsl:choose>
								</div>
							</div>						
						</xsl:otherwise>
						
					</xsl:choose>
					

					
					
					<input type="hidden" class="inputUriEntryPoint" name="inputUriEntryPoint" value="{$uri_entrypoint}"/>
					<div class="form-group">
						<div class="col-lg-offset-1 col-lg-10">
							<xsl:choose>
								<xsl:when test="count($schedule)=0">
									<button type="button" class="btn btn-sm btn-warning create_button" data-reqid="{$guidLegalEntityRequirement}" data-reportid="{guid}">Setup project</button>
									<span style="padding-left: 30px;">(will not be linked to the filing calendar)</span>
								</xsl:when>
								<xsl:otherwise>
									<button type="button" class="btn btn-sm btn-primary create_button" data-reqid="{$guidLegalEntityRequirement}" data-reportid="{guid}">Setup project</button>
								</xsl:otherwise>
							</xsl:choose>
						</div>
					</div>
				</form>
			</div>

		</div>

	</xsl:template>

</xsl:stylesheet>
